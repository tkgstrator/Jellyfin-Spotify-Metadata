using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Catalog;
using Jellyfin.Plugin.Spotify.Catalog.WebPlay;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Spotify.Tests;

public sealed class LiveWebPlayCatalogTests
{
    private const string TrackId = "4uLU6hMCjMI75M1A2tKUQC";
    private const string AlbumId = "6eUW0wxWtzkFdaEFsTJto6";
    private const string ArtistId = "0gxyHStUsqpMadRV0Di1Qt";

    [Fact(Explicit = true)]
    public async Task GetsKnownPublicEntities()
    {
        using var httpClient = new HttpClient();
        var catalog = new WebPlayCatalog(
            new UnusedSearchTransport(),
            new WebPlayEntityTransport(httpClient),
            () => new CatalogOptions(),
            NullLogger<WebPlayCatalog>.Instance);

        var track = await catalog.GetSongAsync(TrackId, CancellationToken.None);
        Assert.NotNull(track);
        Assert.Equal(TrackId, track.Id);
        Assert.Equal("Never Gonna Give You Up", track.Name);
        Assert.NotEmpty(track.Artists);
        Assert.Equal(ArtistId, track.Artists[0].Id);
        Assert.NotNull(track.Album);
        Assert.False(string.IsNullOrWhiteSpace(track.Album.Id));
        Assert.False(string.IsNullOrWhiteSpace(track.Album.Name));
        Assert.NotEmpty(track.Album.Images);

        var album = await catalog.GetAlbumAsync(AlbumId, CancellationToken.None);
        Assert.NotNull(album);
        Assert.Equal(AlbumId, album.Id);
        Assert.False(string.IsNullOrWhiteSpace(album.Name));
        Assert.NotEmpty(album.Artists);
        Assert.All(album.Artists, artist => Assert.False(string.IsNullOrWhiteSpace(artist.Id)));
        Assert.NotEmpty(album.Images);
        Assert.NotEmpty(album.Tracks);
        Assert.All(album.Tracks, item => Assert.False(string.IsNullOrWhiteSpace(item.Id)));

        var artist = await catalog.GetArtistAsync(ArtistId, CancellationToken.None);
        Assert.NotNull(artist);
        Assert.Equal(ArtistId, artist.Id);
        Assert.Equal("Rick Astley", artist.Name);
        Assert.NotEmpty(artist.Images);
    }

    private sealed class UnusedSearchTransport : IWebPlayTransport
    {
        public Task<string> PostAsync(
            string operationName,
            string sha256Hash,
            JsonElement variables,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Entity lookup must not use GraphQL search.");
    }
}
