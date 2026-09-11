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
/// Supplies track metadata from Spotify.
/// </summary>
public class SongMetadataProvider : IRemoteMetadataProvider<Audio, SongInfo>
{
    private readonly ISpotifyCatalog _catalog;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SongMetadataProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SongMetadataProvider"/> class.
    /// </summary>
    /// <param name="catalog">Spotify catalog.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public SongMetadataProvider(
        ISpotifyCatalog catalog,
        IHttpClientFactory httpClientFactory,
        ILogger<SongMetadataProvider> logger)
    {
        _catalog = catalog;
        _httpClient = httpClientFactory.CreateClient(NamedClient.Default);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => PluginConstants.Name;

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        SongInfo searchInfo,
        CancellationToken cancellationToken)
    {
        var tracks = await FindAsync(searchInfo, cancellationToken).ConfigureAwait(false);
        return tracks.Select(ToSearchResult);
    }

    /// <inheritdoc />
    public async Task<MetadataResult<Audio>> GetMetadata(SongInfo info, CancellationToken cancellationToken)
    {
        var tracks = await FindAsync(info, cancellationToken).ConfigureAwait(false);
        if (tracks.Count == 0)
        {
            _logger.LogDebug("No Spotify track for {Name}", info.Name);
            return new MetadataResult<Audio> { HasMetadata = false };
        }

        var track = tracks[0];
        var artists = track.Artists.Select(artist => artist.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
        var item = new Audio
        {
            Name = track.Name,
            Album = track.Album?.Name,
            Artists = artists,
            AlbumArtists = artists,
            IndexNumber = track.TrackNumber > 0 ? track.TrackNumber : null,
            ParentIndexNumber = track.DiscNumber > 0 ? track.DiscNumber : null,
            RunTimeTicks = track.DurationMs > 0 ? TimeSpan.FromMilliseconds(track.DurationMs).Ticks : null,
        };

        var released = AlbumMetadataProvider.ParseReleaseDate(track.Album?.ReleaseDate);
        if (released is not null)
        {
            item.PremiereDate = released;
            item.ProductionYear = released.Value.Year;
        }

        item.SetProviderId(ProviderKeys.Song, track.Id);
        if (!string.IsNullOrWhiteSpace(track.Album?.Id))
        {
            item.SetProviderId(ProviderKeys.Album, track.Album.Id);
        }

        return new MetadataResult<Audio> { Item = item, HasMetadata = true };
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClient.GetAsync(new Uri(url), cancellationToken);

    /// <summary>
    /// Builds the artist, album, and title search term.
    /// </summary>
    /// <param name="info">Lookup information.</param>
    /// <returns>Search term.</returns>
    internal static string BuildSearchTerm(SongInfo info)
    {
        var artist = info.AlbumArtists.Count > 0
            ? info.AlbumArtists[0]
            : info.Artists.Count > 0 ? info.Artists[0] : string.Empty;
        return AlbumMetadataProvider.JoinSearchParts(artist, info.Album, info.Name);
    }

    private static RemoteSearchResult ToSearchResult(Track track)
    {
        var image = ArtworkSelector.Select(track.Album?.Images, GetArtworkSize());
        var result = new RemoteSearchResult
        {
            Name = track.Name,
            ImageUrl = image?.Url,
            SearchProviderName = PluginConstants.Name,
            ProductionYear = AlbumMetadataProvider.ParseReleaseDate(track.Album?.ReleaseDate)?.Year,
        };
        result.SetProviderId(ProviderKeys.Song, track.Id);
        if (!string.IsNullOrWhiteSpace(track.Album?.Id))
        {
            result.SetProviderId(ProviderKeys.Album, track.Album.Id);
        }

        return result;
    }

    private async Task<IReadOnlyList<Track>> FindAsync(SongInfo info, CancellationToken cancellationToken)
    {
        var id = info.GetProviderId(ProviderKeys.Song);
        if (!string.IsNullOrWhiteSpace(id))
        {
            _logger.LogDebug("Looking up Spotify track by id {Id}", id);
            var track = await _catalog.GetSongAsync(id, cancellationToken).ConfigureAwait(false);
            return track is null ? [] : [track];
        }

        var term = BuildSearchTerm(info);
        _logger.LogDebug("Searching Spotify tracks for {Term}", term);
        return await _catalog.SearchSongsAsync(term, cancellationToken).ConfigureAwait(false);
    }

    private static int GetArtworkSize() => Plugin.Instance?.Configuration.ArtworkSize ?? new CatalogOptions().ArtworkSize;
}
