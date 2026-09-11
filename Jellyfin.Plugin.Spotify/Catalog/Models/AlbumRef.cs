using System.Collections.Generic;

namespace Jellyfin.Plugin.Spotify.Catalog.Models;

/// <summary>
/// A compact reference to a Spotify album.
/// </summary>
public sealed class AlbumRef
{
    /// <summary>
    /// Gets or initializes the Spotify album identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the album name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the album release date as returned by Spotify.
    /// </summary>
    public string? ReleaseDate { get; init; }

    /// <summary>
    /// Gets or initializes the album images.
    /// </summary>
    public IReadOnlyList<Image> Images { get; init; } = [];
}
