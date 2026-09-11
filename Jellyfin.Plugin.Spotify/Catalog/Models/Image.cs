namespace Jellyfin.Plugin.Spotify.Catalog.Models;

/// <summary>
/// An image supplied by Spotify.
/// </summary>
public sealed class Image
{
    /// <summary>
    /// Gets or initializes the image URL.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the image width in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets or initializes the image height in pixels.
    /// </summary>
    public int Height { get; init; }
}
