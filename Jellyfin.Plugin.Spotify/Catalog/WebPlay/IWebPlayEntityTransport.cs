using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Spotify.Catalog.WebPlay;

/// <summary>
/// Retrieves decoded entity state from a public Spotify page.
/// </summary>
public interface IWebPlayEntityTransport
{
    /// <summary>
    /// Gets the decoded initial-state JSON for an entity.
    /// </summary>
    /// <param name="entityType">Entity type: track, album, or artist.</param>
    /// <param name="id">Spotify identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decoded JSON, or <see langword="null"/> when the page returns 404.</returns>
    Task<string?> GetAsync(string entityType, string id, CancellationToken cancellationToken);
}
