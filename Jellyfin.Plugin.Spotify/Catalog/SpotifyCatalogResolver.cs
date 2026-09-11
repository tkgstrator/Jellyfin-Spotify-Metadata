using System;

namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// Resolves the explicitly configured Spotify catalog source.
/// </summary>
public sealed class SpotifyCatalogResolver : ISpotifyCatalog
{
    private readonly ISpotifyCatalog _official;
    private readonly ISpotifyCatalog _webPlay;
    private readonly Func<CatalogSource> _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpotifyCatalogResolver"/> class.
    /// </summary>
    /// <param name="official">Official Web API catalog.</param>
    /// <param name="webPlay">Web Player catalog.</param>
    /// <param name="source">Supplies the currently selected source.</param>
    public SpotifyCatalogResolver(ISpotifyCatalog official, ISpotifyCatalog webPlay, Func<CatalogSource> source)
    {
        _official = official;
        _webPlay = webPlay;
        _source = source;
    }

    private ISpotifyCatalog Current => _source() switch
    {
        CatalogSource.Official => _official,
        CatalogSource.WebPlay => _webPlay,
        _ => throw new InvalidOperationException("The configured Spotify catalog source is not supported."),
    };

    /// <inheritdoc />
    public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<Models.Track>> SearchSongsAsync(
        string term,
        System.Threading.CancellationToken cancellationToken)
        => Current.SearchSongsAsync(term, cancellationToken);

    /// <inheritdoc />
    public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<Models.Album>> SearchAlbumsAsync(
        string term,
        System.Threading.CancellationToken cancellationToken)
        => Current.SearchAlbumsAsync(term, cancellationToken);

    /// <inheritdoc />
    public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<Models.Artist>> SearchArtistsAsync(
        string term,
        System.Threading.CancellationToken cancellationToken)
        => Current.SearchArtistsAsync(term, cancellationToken);

    /// <inheritdoc />
    public System.Threading.Tasks.Task<Models.Track?> GetSongAsync(string id, System.Threading.CancellationToken cancellationToken)
        => Current.GetSongAsync(id, cancellationToken);

    /// <inheritdoc />
    public System.Threading.Tasks.Task<Models.Album?> GetAlbumAsync(string id, System.Threading.CancellationToken cancellationToken)
        => Current.GetAlbumAsync(id, cancellationToken);

    /// <inheritdoc />
    public System.Threading.Tasks.Task<Models.Artist?> GetArtistAsync(string id, System.Threading.CancellationToken cancellationToken)
        => Current.GetArtistAsync(id, cancellationToken);
}
