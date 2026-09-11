namespace Jellyfin.Plugin.Spotify.Catalog.Models;

/// <summary>
/// A compact reference to a Spotify artist.
/// </summary>
public sealed class ArtistRef
{
    /// <summary>
    /// Gets or initializes the Spotify artist identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the artist name.
    /// </summary>
    public string Name { get; init; } = string.Empty;
}
