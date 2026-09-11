using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Spotify.Catalog.WebPlay;

/// <summary>
/// Supplies the short-lived public Web Player credentials needed by Pathfinder.
/// </summary>
/// <remarks>
/// Implementations must obtain these values outside this repository. The public
/// static analysis does not provide a safe way to reproduce Spotify's TOTP-based
/// token bootstrap without storing private, obfuscated material.
/// </remarks>
public interface IWebPlaySessionProvider
{
    /// <summary>
    /// Gets a usable anonymous Web Player session.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current session.</returns>
    Task<WebPlaySession> GetSessionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates a rejected session.
    /// </summary>
    void Invalidate();
}
