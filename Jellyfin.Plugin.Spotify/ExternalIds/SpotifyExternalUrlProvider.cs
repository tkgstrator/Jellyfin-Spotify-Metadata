using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.Spotify.Catalog;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Spotify.ExternalIds;

/// <summary>
/// Builds the public Spotify links shown on an item.
/// </summary>
/// <remarks>
/// A separate provider rather than a format string on the external ids,
/// because the link is built from the identifier alone, and
/// the two are stored under different keys.
/// </remarks>
public class SpotifyExternalUrlProvider : IExternalUrlProvider
{
    /// <inheritdoc />
    public string Name => PluginConstants.Name;

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        var key = item switch
        {
            MusicAlbum => ProviderKeys.Album,
            MusicArtist => ProviderKeys.Artist,
            Audio => ProviderKeys.Song,
            _ => null
        };

        if (key is null)
        {
            yield break;
        }

        var id = item.GetProviderId(key);
        if (string.IsNullOrEmpty(id))
        {
            yield break;
        }

        var path = key switch
        {
            ProviderKeys.Album => "album",
            ProviderKeys.Artist => "artist",
            _ => "track"
        };

        yield return string.Format(
            CultureInfo.InvariantCulture,
            "{0}/{1}/{2}",
            PluginConstants.WebBaseUrl,
            path,
            id);
    }
}
