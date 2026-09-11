using System.Collections.Generic;

namespace Jellyfin.Plugin.Spotify.Catalog.Models;

/// <summary>
/// An album in the canonical catalog model.
/// </summary>
public sealed class Album
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
    /// Gets or initializes the album type.
    /// </summary>
    public string? AlbumType { get; init; }

    /// <summary>
    /// Gets or initializes the release date as returned by Spotify.
    /// </summary>
    public string? ReleaseDate { get; init; }

    /// <summary>
    /// Gets or initializes the label.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Gets or initializes the copyright statements.
    /// </summary>
    public IReadOnlyList<string> Copyrights { get; init; } = [];

    /// <summary>
    /// Gets or initializes the album artists.
    /// </summary>
    public IReadOnlyList<ArtistRef> Artists { get; init; } = [];

    /// <summary>
    /// Gets or initializes the album images.
    /// </summary>
    public IReadOnlyList<Image> Images { get; init; } = [];

    /// <summary>
    /// Gets or initializes the album tracks in catalog order.
    /// </summary>
    public IReadOnlyList<Track> Tracks { get; init; } = [];
}
