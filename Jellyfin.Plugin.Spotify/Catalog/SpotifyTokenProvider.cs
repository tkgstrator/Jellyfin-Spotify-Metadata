using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// Exchanges a client id and secret for an access token, using the client
/// credentials flow.
/// </summary>
/// <remarks>
/// <para>
/// Client credentials is the right flow here: the plugin reads the public
/// catalog on the server's own behalf and never acts as a listener, so there
/// is no user to send through a consent screen and no refresh token to keep.
/// </para>
/// <para>
/// A token lasts an hour. It is refreshed a minute early rather than on
/// expiry, so a request already in flight cannot cross the boundary, and the
/// fetch is serialised so a burst of lookups asks for one token, not twenty.
/// </para>
/// </remarks>
public sealed class SpotifyTokenProvider : ISpotifyTokenProvider, IDisposable
{
    /// <summary>
    /// How long before the stated expiry a token is treated as spent.
    /// </summary>
    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _httpClient;
    private readonly Func<SpotifyCredentials> _credentials;
    private readonly ILogger<SpotifyTokenProvider> _logger;
    private readonly TimeProvider _time;

    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpotifyTokenProvider"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="credentials">
    /// Supplies the current credentials. A delegate rather than a value,
    /// because the user can change them while the server is running.
    /// </param>
    /// <param name="logger">Logger.</param>
    /// <param name="time">Clock; defaults to the system clock.</param>
    public SpotifyTokenProvider(
        HttpClient httpClient,
        Func<SpotifyCredentials> credentials,
        ILogger<SpotifyTokenProvider> logger,
        TimeProvider? time = null)
    {
        _httpClient = httpClient;
        _credentials = credentials;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public void Dispose()
        => _gate.Dispose();

    /// <inheritdoc />
    public void Invalidate()
    {
        _token = null;
        _expiresAt = DateTimeOffset.MinValue;
    }

    /// <inheritdoc />
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (IsUsable())
        {
            return _token!;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have fetched one while this call queued.
            if (IsUsable())
            {
                return _token!;
            }

            var credentials = _credentials();
            if (string.IsNullOrWhiteSpace(credentials.ClientId) || string.IsNullOrWhiteSpace(credentials.ClientSecret))
            {
                throw new InvalidOperationException(
                    "Spotify client id and secret are not configured; set them on the plugin's settings page.");
            }

            var (token, lifetime) = await RequestTokenAsync(credentials, cancellationToken).ConfigureAwait(false);
            _token = token;
            _expiresAt = _time.GetUtcNow() + lifetime;
            _logger.LogDebug("Spotify access token acquired, valid until {ExpiresAt}", _expiresAt);
            return token;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Reads the token and its lifetime out of a token response.
    /// </summary>
    /// <param name="json">Response body.</param>
    /// <returns>The token and how long it lasts.</returns>
    /// <exception cref="CatalogAuthenticationException">The body carried no token.</exception>
    internal static (string Token, TimeSpan Lifetime) ParseToken(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("access_token", out var token) || token.ValueKind != JsonValueKind.String)
            {
                throw new CatalogAuthenticationException("The Spotify token response carried no access_token.");
            }

            var seconds = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var value)
                ? value
                : 3600;
            return (token.GetString()!, TimeSpan.FromSeconds(seconds));
        }
        catch (JsonException ex)
        {
            throw new CatalogAuthenticationException("The Spotify token response was not valid JSON.", ex);
        }
    }

    private bool IsUsable()
        => !string.IsNullOrEmpty(_token) && _time.GetUtcNow() + Margin < _expiresAt;

    private async Task<(string Token, TimeSpan Lifetime)> RequestTokenAsync(
        SpotifyCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, PluginConstants.TokenUrl);

        // The credentials go in the Authorization header rather than the body:
        // both are accepted, and this keeps them out of any form-encoded log.
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            string.Format(CultureInfo.InvariantCulture, "{0}:{1}", credentials.ClientId, credentials.ClientSecret)));
        request.Headers.TryAddWithoutValidation("Authorization", "Basic " + basic);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new CatalogAuthenticationException(string.Format(
                CultureInfo.InvariantCulture,
                "Spotify refused the client credentials with {0}.",
                (int)response.StatusCode));
        }

        return ParseToken(body);
    }
}
