using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// Supplies an access token for the Spotify Web API.
/// </summary>
public interface ISpotifyTokenProvider
{
    /// <summary>
    /// Gets a usable access token, fetching a new one when the current one is
    /// missing or about to expire.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The access token.</returns>
    Task<string> GetTokenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Discards the current token so the next call fetches a fresh one. Called
    /// when the API rejects a token that had not yet expired.
    /// </summary>
    void Invalidate();
}
