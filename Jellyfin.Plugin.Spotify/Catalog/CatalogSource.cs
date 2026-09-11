namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// Selects the Spotify catalog implementation used by the plugin.
/// </summary>
public enum CatalogSource
{
    /// <summary>
    /// Uses the supported Spotify Web API with client credentials.
    /// </summary>
    Official,

    /// <summary>
    /// Uses the unsupported public Web Player GraphQL search endpoint.
    /// </summary>
    WebPlay,
}
