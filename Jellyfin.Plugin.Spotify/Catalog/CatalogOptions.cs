namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// Catalog lookup settings. Deliberately free of any Jellyfin dependency so the
/// catalog layer stays unit testable.
/// </summary>
public class CatalogOptions
{
    /// <summary>
    /// Gets or sets the market to look items up in, as an ISO 3166-1 alpha-2
    /// country code.
    /// </summary>
    /// <remarks>
    /// Spotify uses the market to decide what is available and which relinked
    /// track an id resolves to. It does not scope the identifier itself, so it
    /// is a single value rather than an ordered fallback list.
    /// </remarks>
    public string Market { get; set; } = "JP";

    /// <summary>
    /// Gets or sets the maximum number of results requested per search.
    /// Spotify caps this at 50.
    /// </summary>
    public int MaxSearchResults { get; set; } = 25;

    /// <summary>
    /// Gets or sets the edge length in pixels used when picking artwork.
    /// </summary>
    /// <remarks>
    /// Spotify returns a fixed set of image sizes rather than a template, so
    /// this picks the closest one instead of resizing.
    /// </remarks>
    public int ArtworkSize { get; set; } = 640;
}
