using System.Collections.Generic;

namespace Jellyfin.Plugin.Spotify.Catalog.Models;

/// <summary>
/// A track in the canonical catalog model.
/// </summary>
public sealed class Track
{
    /// <summary>
    /// Gets or initializes the Spotify track identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the track name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the track number within its disc.
    /// </summary>
    public int TrackNumber { get; init; }

    /// <summary>
    /// Gets or initializes the disc number.
    /// </summary>
    public int DiscNumber { get; init; }

    /// <summary>
    /// Gets or initializes the duration in milliseconds.
    /// </summary>
    public int DurationMs { get; init; }

    /// <summary>
    /// Gets a value indicating whether the track is explicit.
    /// </summary>
    public bool Explicit { get; init; }

    /// <summary>
    /// Gets or initializes the International Standard Recording Code.
    /// </summary>
    public string? Isrc { get; init; }

    /// <summary>
    /// Gets or initializes the track artists.
    /// </summary>
    public IReadOnlyList<ArtistRef> Artists { get; init; } = [];

    /// <summary>
    /// Gets or initializes the containing album, when supplied by Spotify.
    /// </summary>
    public AlbumRef? Album { get; init; }
}
