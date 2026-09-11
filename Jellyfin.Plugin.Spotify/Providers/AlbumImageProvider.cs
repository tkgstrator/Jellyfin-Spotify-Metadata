using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Catalog;
using Jellyfin.Plugin.Spotify.Catalog.Models;
using Jellyfin.Plugin.Spotify.ExternalIds;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Spotify.Providers;

/// <summary>
/// Supplies album artwork from Spotify.
/// </summary>
public class AlbumImageProvider : IRemoteImageProvider
{
    private readonly ISpotifyCatalog _catalog;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AlbumImageProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlbumImageProvider"/> class.
    /// </summary>
    /// <param name="catalog">Spotify catalog.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public AlbumImageProvider(
        ISpotifyCatalog catalog,
        IHttpClientFactory httpClientFactory,
        ILogger<AlbumImageProvider> logger)
    {
        _catalog = catalog;
        _httpClient = httpClientFactory.CreateClient(NamedClient.Default);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => PluginConstants.Name;

    /// <inheritdoc />
    public bool Supports(BaseItem item) => item is MusicAlbum;

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => [ImageType.Primary];

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClient.GetAsync(new Uri(url), cancellationToken);

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is not MusicAlbum album)
        {
            return [];
        }

        var id = album.GetProviderId(ProviderKeys.Album);
        if (!string.IsNullOrWhiteSpace(id))
        {
            var found = await _catalog.GetAlbumAsync(id, cancellationToken).ConfigureAwait(false);
            return ToImageInfos(found is null ? [] : found.Images);
        }

        var artist = album.AlbumArtists.Count > 0 ? album.AlbumArtists[0] : string.Empty;
        var term = AlbumMetadataProvider.JoinSearchParts(artist, album.Name);
        _logger.LogDebug("Searching Spotify album artwork for {Term}", term);
        var albums = await _catalog.SearchAlbumsAsync(term, cancellationToken).ConfigureAwait(false);
        return albums.SelectMany(candidate => ToImageInfos(candidate.Images)).ToList();
    }

    private static IEnumerable<RemoteImageInfo> ToImageInfos(IReadOnlyList<Image> images)
    {
        var image = ArtworkSelector.Select(images, GetArtworkSize());
        return image is null
            ? []
            :
            [
                new RemoteImageInfo
                {
                    ProviderName = PluginConstants.Name,
                    Type = ImageType.Primary,
                    Url = image.Url,
                    Width = image.Width,
                    Height = image.Height,
                    ThumbnailUrl = image.Url,
                }
            ];
    }

    private static int GetArtworkSize() => Plugin.Instance?.Configuration.ArtworkSize ?? new CatalogOptions().ArtworkSize;
}
