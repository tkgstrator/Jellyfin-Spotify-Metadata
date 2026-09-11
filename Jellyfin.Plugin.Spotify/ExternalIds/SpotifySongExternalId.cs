using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Spotify.ExternalIds;

/// <summary>
/// External link to a song on Spotify.
/// </summary>
public class SpotifySongExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => PluginConstants.Name;

    /// <inheritdoc />
    public string Key => ProviderKeys.Song;

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Track;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is Audio;
}
