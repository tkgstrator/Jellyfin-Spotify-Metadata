namespace Jellyfin.Plugin.Spotify;

/// <summary>
/// Names and URLs shared across the plugin.
/// </summary>
public static class PluginConstants
{
    /// <summary>
    /// Display name, also used as the provider name Jellyfin shows in library
    /// settings and on external links.
    /// </summary>
    public const string Name = "Spotify";

    /// <summary>
    /// Base URL of the public Spotify web player. Identifiers are global, so a
    /// link needs nothing but the id.
    /// </summary>
    public const string WebBaseUrl = "https://open.spotify.com";

    /// <summary>
    /// Base URL of the Spotify Web API.
    /// </summary>
    public const string ApiBaseUrl = "https://api.spotify.com/v1";

    /// <summary>
    /// Endpoint that exchanges client credentials for an access token.
    /// </summary>
    public const string TokenUrl = "https://accounts.spotify.com/api/token";
}
