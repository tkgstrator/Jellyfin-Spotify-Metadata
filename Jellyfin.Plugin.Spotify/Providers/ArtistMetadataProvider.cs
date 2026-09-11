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
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Spotify.Providers;

/// <summary>
/// Supplies artist metadata from Spotify.
/// </summary>
public class ArtistMetadataProvider : IRemoteMetadataProvider<MusicArtist, ArtistInfo>
{
    private readonly ISpotifyCatalog _catalog;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ArtistMetadataProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtistMetadataProvider"/> class.
    /// </summary>
    /// <param name="catalog">Spotify catalog.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public ArtistMetadataProvider(
        ISpotifyCatalog catalog,
        IHttpClientFactory httpClientFactory,
        ILogger<ArtistMetadataProvider> logger)
    {
        _catalog = catalog;
        _httpClient = httpClientFactory.CreateClient(NamedClient.Default);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => PluginConstants.Name;

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        ArtistInfo searchInfo,
        CancellationToken cancellationToken)
    {
        var artists = await FindAsync(searchInfo, cancellationToken).ConfigureAwait(false);
        return artists.Select(ToSearchResult);
    }

    /// <inheritdoc />
    public async Task<MetadataResult<MusicArtist>> GetMetadata(ArtistInfo info, CancellationToken cancellationToken)
    {
        var artists = await FindAsync(info, cancellationToken).ConfigureAwait(false);
        if (artists.Count == 0)
        {
            _logger.LogDebug("No Spotify artist for {Name}", info.Name);
            return new MetadataResult<MusicArtist> { HasMetadata = false };
        }

        var artist = artists[0];
        var item = new MusicArtist { Name = artist.Name };
        foreach (var genre in artist.Genres.Where(genre => !string.IsNullOrWhiteSpace(genre)))
        {
            item.AddGenre(genre);
        }

        item.SetProviderId(ProviderKeys.Artist, artist.Id);
        return new MetadataResult<MusicArtist> { Item = item, HasMetadata = true };
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClient.GetAsync(new Uri(url), cancellationToken);

    private static RemoteSearchResult ToSearchResult(Artist artist)
    {
        var image = ArtworkSelector.Select(artist.Images, GetArtworkSize());
        var result = new RemoteSearchResult
        {
            Name = artist.Name,
            ImageUrl = image?.Url,
            SearchProviderName = PluginConstants.Name,
        };
        result.SetProviderId(ProviderKeys.Artist, artist.Id);
        return result;
    }

    private async Task<IReadOnlyList<Artist>> FindAsync(ArtistInfo info, CancellationToken cancellationToken)
    {
        var id = info.GetProviderId(ProviderKeys.Artist);
        if (!string.IsNullOrWhiteSpace(id))
        {
            _logger.LogDebug("Looking up Spotify artist by id {Id}", id);
            var artist = await _catalog.GetArtistAsync(id, cancellationToken).ConfigureAwait(false);
            return artist is null ? [] : [artist];
        }

        _logger.LogDebug("Searching Spotify artists for {Term}", info.Name);
        return await _catalog.SearchArtistsAsync(info.Name, cancellationToken).ConfigureAwait(false);
    }

    private static int GetArtworkSize() => Plugin.Instance?.Configuration.ArtworkSize ?? new CatalogOptions().ArtworkSize;
}
