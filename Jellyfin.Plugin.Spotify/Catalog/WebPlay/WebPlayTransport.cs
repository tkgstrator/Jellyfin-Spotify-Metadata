using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Spotify.Catalog.WebPlay;

/// <summary>
/// Posts persisted GraphQL queries to Spotify's Web Player Pathfinder endpoint.
/// </summary>
public sealed class WebPlayTransport : IWebPlayTransport
{
    /// <summary>
    /// The Web Player Pathfinder endpoint observed in the public client.
    /// </summary>
    public const string Endpoint = "https://api-partner.spotify.com/pathfinder/v1/query";

    private readonly HttpClient _httpClient;
    private readonly IWebPlaySessionProvider _sessionProvider;
    private readonly ILogger<WebPlayTransport> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebPlayTransport"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="sessionProvider">Anonymous Web Player session provider.</param>
    /// <param name="logger">Logger.</param>
    public WebPlayTransport(
        HttpClient httpClient,
        IWebPlaySessionProvider sessionProvider,
        ILogger<WebPlayTransport> logger)
    {
        _httpClient = httpClient;
        _sessionProvider = sessionProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> PostAsync(
        string operationName,
        string sha256Hash,
        JsonElement variables,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var session = await _sessionProvider.GetSessionAsync(cancellationToken).ConfigureAwait(false);
            session.EnsureValid();
            var payload = JsonSerializer.Serialize(
                new
                {
                    operationName,
                    variables,
                    extensions = new
                    {
                        persistedQuery = new { version = 1, sha256Hash },
                    },
                },
                CatalogJson.Options);

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + session.AccessToken);
            request.Headers.TryAddWithoutValidation("App-Platform", "WebPlayer");
            request.Headers.TryAddWithoutValidation("Spotify-App-Version", session.ClientVersion);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            _logger.LogDebug("POST WebPlay operation {OperationName}", operationName);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 1)
            {
                _sessionProvider.Invalidate();
                continue;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new CatalogRateLimitedException(ReadRetryAfter(response));
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new CatalogAuthenticationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Spotify Web Player answered {0} for {1}; the anonymous session may have expired.",
                    (int)response.StatusCode,
                    operationName));
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is not null)
        {
            return header.Delta;
        }

        return header?.Date is not null ? header.Date - DateTimeOffset.UtcNow : null;
    }
}
