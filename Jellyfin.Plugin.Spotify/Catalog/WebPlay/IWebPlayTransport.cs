using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Spotify.Catalog.WebPlay;

/// <summary>
/// Carries persisted GraphQL operations to the Spotify Web Player endpoint.
/// </summary>
public interface IWebPlayTransport
{
    /// <summary>
    /// Posts one persisted operation.
    /// </summary>
    /// <param name="operationName">GraphQL operation name.</param>
    /// <param name="sha256Hash">Persisted-query SHA-256 hash.</param>
    /// <param name="variables">Operation variables.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw GraphQL response body.</returns>
    Task<string> PostAsync(
        string operationName,
        string sha256Hash,
        JsonElement variables,
        CancellationToken cancellationToken);
}
