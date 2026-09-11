using System;
using System.Collections.Generic;
using System.Globalization;
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
/// Supplies album metadata from Spotify.
/// </summary>
public class AlbumMetadataProvider : IRemoteMetadataProvider<MusicAlbum, AlbumInfo>
{
    private readonly ISpotifyCatalog _catalog;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AlbumMetadataProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlbumMetadataProvider"/> class.
    /// </summary>
    /// <param name="catalog">Spotify catalog.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public AlbumMetadataProvider(
        ISpotifyCatalog catalog,
        IHttpClientFactory httpClientFactory,
        ILogger<AlbumMetadataProvider> logger)
    {
        _catalog = catalog;
        _httpClient = httpClientFactory.CreateClient(NamedClient.Default);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => PluginConstants.Name;

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        AlbumInfo searchInfo,
        CancellationToken cancellationToken)
    {
        var albums = await FindAsync(searchInfo, cancellationToken).ConfigureAwait(false);
        return albums.Select(ToSearchResult);
    }

    /// <inheritdoc />
    public async Task<MetadataResult<MusicAlbum>> GetMetadata(AlbumInfo info, CancellationToken cancellationToken)
    {
        var albums = await FindAsync(info, cancellationToken).ConfigureAwait(false);
        if (albums.Count == 0)
        {
            _logger.LogDebug("No Spotify album for {Name}", info.Name);
            return new MetadataResult<MusicAlbum> { HasMetadata = false };
        }

        var album = albums[0];
        var artists = album.Artists.Select(artist => artist.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
        var item = new MusicAlbum
        {
            Name = album.Name,
            AlbumArtists = artists,
            Artists = artists,
        };

        var released = ParseReleaseDate(album.ReleaseDate);
        if (released is not null)
        {
            item.PremiereDate = released;
            item.ProductionYear = released.Value.Year;
        }

        if (!string.IsNullOrWhiteSpace(album.Label))
        {
            item.AddStudio(album.Label);
        }

        item.SetProviderId(ProviderKeys.Album, album.Id);
        return new MetadataResult<MusicAlbum> { Item = item, HasMetadata = true };
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClient.GetAsync(new Uri(url), cancellationToken);

    /// <summary>
    /// Parses Spotify's year, month, or day precision release date.
    /// </summary>
    /// <param name="value">Raw release date.</param>
    /// <returns>The date with omitted components set to one, or null.</returns>
    internal static DateTime? ParseReleaseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var format in new[] { "yyyy-MM-dd", "yyyy-MM", "yyyy" })
        {
            if (DateTime.TryParseExact(
                value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds the artist and album search term.
    /// </summary>
    /// <param name="info">Lookup information.</param>
    /// <returns>Search term.</returns>
    internal static string BuildSearchTerm(AlbumInfo info)
    {
        var artist = info.AlbumArtists.Count > 0 ? info.AlbumArtists[0] : string.Empty;
        return JoinSearchParts(artist, info.Name);
    }

    internal static string JoinSearchParts(params string?[] parts)
        => string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static RemoteSearchResult ToSearchResult(Album album)
    {
        var image = ArtworkSelector.Select(album.Images, GetArtworkSize());
        var result = new RemoteSearchResult
        {
            Name = album.Name,
            ImageUrl = image?.Url,
            SearchProviderName = PluginConstants.Name,
            ProductionYear = ParseReleaseDate(album.ReleaseDate)?.Year,
        };
        result.SetProviderId(ProviderKeys.Album, album.Id);
        return result;
    }

    private async Task<IReadOnlyList<Album>> FindAsync(AlbumInfo info, CancellationToken cancellationToken)
    {
        var id = info.GetProviderId(ProviderKeys.Album);
        if (!string.IsNullOrWhiteSpace(id))
        {
            _logger.LogDebug("Looking up Spotify album by id {Id}", id);
            var album = await _catalog.GetAlbumAsync(id, cancellationToken).ConfigureAwait(false);
            return album is null ? [] : [album];
        }

        var term = BuildSearchTerm(info);
        _logger.LogDebug("Searching Spotify albums for {Term}", term);
        return await _catalog.SearchAlbumsAsync(term, cancellationToken).ConfigureAwait(false);
    }

    private static int GetArtworkSize() => Plugin.Instance?.Configuration.ArtworkSize ?? new CatalogOptions().ArtworkSize;
}
