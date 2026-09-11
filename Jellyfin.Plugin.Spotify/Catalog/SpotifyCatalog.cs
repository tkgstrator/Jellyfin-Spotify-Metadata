using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Catalog.Models;
using Jellyfin.Plugin.Spotify.Catalog.Official;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// Reads the Spotify catalog through the official Spotify Web API.
/// </summary>
public sealed class SpotifyCatalog : ISpotifyCatalog
{
    private const int MaxAlbumTrackPages = 20;
    private readonly ICatalogTransport _transport;
    private readonly Func<CatalogOptions> _options;
    private readonly ILogger<SpotifyCatalog> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpotifyCatalog"/> class.
    /// </summary>
    /// <param name="transport">Transport carrying official Web API requests.</param>
    /// <param name="options">Supplies the current catalog options.</param>
    /// <param name="logger">Logger.</param>
    public SpotifyCatalog(
        ICatalogTransport transport,
        Func<CatalogOptions> options,
        ILogger<SpotifyCatalog> logger)
    {
        _transport = transport;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Track>> SearchSongsAsync(string term, CancellationToken cancellationToken)
        => SearchAsync(term, "track", response => response.Tracks?.Items, ConvertTrack, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Album>> SearchAlbumsAsync(string term, CancellationToken cancellationToken)
        => SearchAsync(term, "album", response => response.Albums?.Items, ConvertAlbum, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Artist>> SearchArtistsAsync(string term, CancellationToken cancellationToken)
        => SearchAsync(term, "artist", response => response.Artists?.Items, ConvertArtist, cancellationToken);

    /// <inheritdoc />
    public Task<Track?> GetSongAsync(string id, CancellationToken cancellationToken)
        => GetAsync<TrackObject, Track>(id, "tracks", includeMarket: true, ConvertTrack, cancellationToken);

    /// <inheritdoc />
    public async Task<Album?> GetAlbumAsync(string id, CancellationToken cancellationToken)
    {
        var album = await GetOfficialAsync<AlbumObject>(id, "albums", includeMarket: true, cancellationToken).ConfigureAwait(false);
        if (album is null)
        {
            return null;
        }

        var tracks = album.Tracks is null
            ? []
            : album.Tracks.Items.Where(IsValid).Select(ConvertTrack).ToList();
        var next = album.Tracks?.Next;
        for (var page = 1; !string.IsNullOrWhiteSpace(next) && page < MaxAlbumTrackPages; page++)
        {
            var url = NormalizeNextUrl(next);
            if (url is null)
            {
                _logger.LogError("Spotify returned an invalid album track page URL {Url}", next);
                break;
            }

            var more = await FetchAsync<Page<TrackObject>>(url, cancellationToken).ConfigureAwait(false);
            if (more is null)
            {
                break;
            }

            tracks.AddRange(more.Items.Where(IsValid).Select(ConvertTrack));
            next = more.Next;
        }

        return ConvertAlbum(album, tracks);
    }

    /// <inheritdoc />
    public Task<Artist?> GetArtistAsync(string id, CancellationToken cancellationToken)
        => GetAsync<ArtistObject, Artist>(id, "artists", includeMarket: false, ConvertArtist, cancellationToken);

    private static bool IsValid(TrackObject item) => !string.IsNullOrWhiteSpace(item.Id);

    private static bool IsValid(AlbumObject item) => !string.IsNullOrWhiteSpace(item.Id);

    private static bool IsValid(ArtistObject item) => !string.IsNullOrWhiteSpace(item.Id);

    private static ArtistRef ConvertArtistRef(ArtistReferenceObject artist)
        => new() { Id = artist.Id, Name = artist.Name };

    private static Image ConvertImage(ImageObject image)
        => new() { Url = image.Url, Width = image.Width, Height = image.Height };

    private static AlbumRef ConvertAlbumRef(AlbumReferenceObject album)
        => new()
        {
            Id = album.Id,
            Name = album.Name,
            ReleaseDate = album.ReleaseDate,
            Images = album.Images.Select(ConvertImage).ToList(),
        };

    private static Track ConvertTrack(TrackObject track)
        => new()
        {
            Id = track.Id,
            Name = track.Name,
            TrackNumber = track.TrackNumber,
            DiscNumber = track.DiscNumber,
            DurationMs = track.DurationMs,
            Explicit = track.Explicit,
            Isrc = track.ExternalIds?.Isrc,
            Artists = track.Artists.Where(artist => !string.IsNullOrWhiteSpace(artist.Id)).Select(ConvertArtistRef).ToList(),
            Album = track.Album is null || string.IsNullOrWhiteSpace(track.Album.Id) ? null : ConvertAlbumRef(track.Album),
        };

    private static Album ConvertAlbum(AlbumObject album)
        => ConvertAlbum(album, album.Tracks?.Items.Where(IsValid).Select(ConvertTrack).ToList() ?? []);

    private static Album ConvertAlbum(AlbumObject album, IReadOnlyList<Track> tracks)
        => new()
        {
            Id = album.Id,
            Name = album.Name,
            AlbumType = album.AlbumType,
            ReleaseDate = album.ReleaseDate,
            Label = album.Label,
            Copyrights = album.Copyrights.Select(copyright => copyright.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToList(),
            Artists = album.Artists.Where(artist => !string.IsNullOrWhiteSpace(artist.Id)).Select(ConvertArtistRef).ToList(),
            Images = album.Images.Select(ConvertImage).ToList(),
            Tracks = tracks,
        };

    private static Artist ConvertArtist(ArtistObject artist)
        => new()
        {
            Id = artist.Id,
            Name = artist.Name,
            Genres = artist.Genres,
            Images = artist.Images.Select(ConvertImage).ToList(),
        };

    private async Task<IReadOnlyList<TCanonical>> SearchAsync<TOfficial, TCanonical>(
        string term,
        string type,
        Func<SearchResponse, IReadOnlyList<TOfficial>?> select,
        Func<TOfficial, TCanonical> convert,
        CancellationToken cancellationToken)
        where TOfficial : class
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        var options = _options();
        var url = string.Format(
            CultureInfo.InvariantCulture,
            "/search?q={0}&type={1}&market={2}&limit={3}",
            Uri.EscapeDataString(term),
            type,
            Uri.EscapeDataString(options.Market),
            Math.Clamp(options.MaxSearchResults, 1, 50));
        var response = await FetchAsync<SearchResponse>(url, cancellationToken).ConfigureAwait(false);
        var items = response is null ? null : select(response);
        if (items is null)
        {
            return [];
        }

        return items.Where(item => item switch
        {
            TrackObject track => IsValid(track),
            AlbumObject album => IsValid(album),
            ArtistObject artist => IsValid(artist),
            _ => true,
        }).Select(convert).ToList();
    }

    private async Task<TCanonical?> GetAsync<TOfficial, TCanonical>(
        string id,
        string resource,
        bool includeMarket,
        Func<TOfficial, TCanonical> convert,
        CancellationToken cancellationToken)
        where TOfficial : class
    {
        var item = await GetOfficialAsync<TOfficial>(id, resource, includeMarket, cancellationToken).ConfigureAwait(false);
        return item is null ? default : convert(item);
    }

    private Task<T?> GetOfficialAsync<T>(
        string id,
        string resource,
        bool includeMarket,
        CancellationToken cancellationToken)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.FromResult<T?>(null);
        }

        var url = "/" + resource + "/" + Uri.EscapeDataString(id);
        if (includeMarket)
        {
            url += "?market=" + Uri.EscapeDataString(_options().Market);
        }

        return FetchAsync<T>(url, cancellationToken);
    }

    private async Task<T?> FetchAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        var body = await _transport.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, CatalogJson.Options);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Could not parse the Spotify response from {Url}", url);
            return null;
        }
    }

    private static string? NormalizeNextUrl(string next)
    {
        if (!Uri.TryCreate(next, UriKind.RelativeOrAbsolute, out var uri))
        {
            return null;
        }

        if (!uri.IsAbsoluteUri)
        {
            return next.StartsWith("/v1/", StringComparison.Ordinal) ? next[3..] : next;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, "api.spotify.com", StringComparison.OrdinalIgnoreCase)
            || !uri.AbsolutePath.StartsWith("/v1/", StringComparison.Ordinal))
        {
            return null;
        }

        return uri.PathAndQuery[3..];
    }
}
