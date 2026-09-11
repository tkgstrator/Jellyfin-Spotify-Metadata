using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Spotify.ExternalIds;

/// <summary>
/// External link to an artist on Spotify.
/// </summary>
public class SpotifyArtistExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => PluginConstants.Name;

    /// <inheritdoc />
    public string Key => ProviderKeys.Artist;

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Artist;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is MusicArtist;
}
