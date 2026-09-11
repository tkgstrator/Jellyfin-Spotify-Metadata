using System.Collections.Generic;

namespace Jellyfin.Plugin.Spotify.Catalog.Models;

/// <summary>
/// An artist in the canonical catalog model.
/// </summary>
public sealed class Artist
{
    /// <summary>
    /// Gets or initializes the Spotify artist identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the artist name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the genres associated with the artist.
    /// </summary>
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>
    /// Gets or initializes the artist images.
    /// </summary>
    public IReadOnlyList<Image> Images { get; init; } = [];
}
