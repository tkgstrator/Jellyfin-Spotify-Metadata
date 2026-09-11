using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// Talks to the Spotify Web API, attaching a bearer token to every request.
/// </summary>
/// <remarks>
/// Returns the raw body: deserialisation belongs to the catalog layer, which
/// lets the cache store responses exactly as they arrived.
/// </remarks>
public class SpotifyTransport : ICatalogTransport
{
    private readonly HttpClient _httpClient;
    private readonly ISpotifyTokenProvider _tokenProvider;
    private readonly ILogger<SpotifyTransport> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpotifyTransport"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="tokenProvider">Access token provider.</param>
    /// <param name="logger">Logger.</param>
    public SpotifyTransport(
        HttpClient httpClient,
        ISpotifyTokenProvider tokenProvider,
        ILogger<SpotifyTransport> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        // One retry, and only for 401: a token can be rejected before its
        // stated expiry when the application's secret is rotated.
        for (var attempt = 1; ; attempt++)
        {
            var token = await _tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);

            using var request = new HttpRequestMessage(HttpMethod.Get, PluginConstants.ApiBaseUrl + relativeUrl);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);

            _logger.LogDebug("GET {Url}", relativeUrl);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Not found: {Url}", relativeUrl);
                return null;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 1)
            {
                _logger.LogDebug("Spotify rejected the access token; fetching a fresh one");
                _tokenProvider.Invalidate();
                continue;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // Unlike Apple Music, Spotify says how long to wait. Carry it
                // so the throttle can honour it instead of guessing.
                var retryAfter = ReadRetryAfter(response);
                _logger.LogDebug(
                    "Spotify rate limited {Url}; it asks for {RetryAfter}",
                    relativeUrl,
                    retryAfter);
                throw new CatalogRateLimitedException(retryAfter);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new CatalogAuthenticationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Spotify answered {0} for {1}; check the client id and secret.",
                    (int)response.StatusCode,
                    relativeUrl));
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the <c>Retry-After</c> header, which Spotify sends in seconds.
    /// </summary>
    /// <param name="response">Response carrying the header.</param>
    /// <returns>How long to wait, or null when the header was absent.</returns>
    internal static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header is null)
        {
            return null;
        }

        if (header.Delta is not null)
        {
            return header.Delta;
        }

        return header.Date is not null ? header.Date - DateTimeOffset.UtcNow : null;
    }
}
