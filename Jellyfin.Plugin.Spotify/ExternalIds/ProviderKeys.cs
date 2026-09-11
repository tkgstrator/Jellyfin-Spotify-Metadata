namespace Jellyfin.Plugin.Spotify.ExternalIds;

/// <summary>
/// Keys this plugin writes into <c>ProviderIds</c>.
/// </summary>
public static class ProviderKeys
{
    /// <summary>
    /// Spotify catalog identifier of a song.
    /// </summary>
    public const string Song = "SpotifySong";

    /// <summary>
    /// Spotify catalog identifier of an album.
    /// </summary>
    public const string Album = "SpotifyAlbum";

    /// <summary>
    /// Spotify catalog identifier of an artist.
    /// </summary>
    public const string Artist = "SpotifyArtist";

    // Spotify identifiers are global: the same id names the same resource in
    // every market. A market only decides whether a resource is playable and
    // which relinked track you get, so there is nothing to store alongside an
    // id — unlike Apple Music, where ids are storefront-scoped.
}
