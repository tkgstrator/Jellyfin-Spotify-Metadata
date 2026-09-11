using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Catalog.Models;

namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// Reads tracks, albums, and artists from the Spotify catalog.
/// </summary>
public interface ISpotifyCatalog
{
    /// <summary>
    /// Searches for tracks.
    /// </summary>
    /// <param name="term">Search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching tracks.</returns>
    Task<IReadOnlyList<Track>> SearchSongsAsync(string term, CancellationToken cancellationToken);

    /// <summary>
    /// Searches for albums.
    /// </summary>
    /// <param name="term">Search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching albums.</returns>
    Task<IReadOnlyList<Album>> SearchAlbumsAsync(string term, CancellationToken cancellationToken);

    /// <summary>
    /// Searches for artists.
    /// </summary>
    /// <param name="term">Search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching artists.</returns>
    Task<IReadOnlyList<Artist>> SearchArtistsAsync(string term, CancellationToken cancellationToken);

    /// <summary>
    /// Looks a track up by its world-wide Spotify identifier.
    /// </summary>
    /// <param name="id">Spotify track identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The track, or null when it was not found.</returns>
    Task<Track?> GetSongAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Looks an album up by its world-wide Spotify identifier.
    /// </summary>
    /// <param name="id">Spotify album identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The album, or null when it was not found.</returns>
    Task<Album?> GetAlbumAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Looks an artist up by its world-wide Spotify identifier.
    /// </summary>
    /// <param name="id">Spotify artist identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The artist, or null when it was not found.</returns>
    Task<Artist?> GetArtistAsync(string id, CancellationToken cancellationToken);
}
