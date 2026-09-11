using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Catalog;
using Jellyfin.Plugin.Spotify.Catalog.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Spotify.Tests;

public class SpotifyCatalogTests
{
    [Fact]
    public async Task SearchSongsAsync_UsesOfficialEndpointAndMapsResponse()
    {
        var transport = new StubTransport(
            """{"tracks":{"items":[{"id":"t1","name":"Song","track_number":3,"disc_number":2,"duration_ms":1234,"explicit":true,"external_ids":{"isrc":"JPABC"},"artists":[{"id":"a1","name":"Artist"}],"album":{"id":"b1","name":"Album","release_date":"2026-01-02","images":[{"url":"https://image","width":640,"height":640}]}}]}}""");
        var catalog = Build(transport, new CatalogOptions { Market = "US", MaxSearchResults = 99 });

        var track = Assert.Single(await catalog.SearchSongsAsync("a & b", CancellationToken.None));

        Assert.Equal("/search?q=a%20%26%20b&type=track&market=US&limit=50", Assert.Single(transport.Urls));
        Assert.Equal("t1", track.Id);
        Assert.Equal("JPABC", track.Isrc);
        Assert.Equal("a1", Assert.Single(track.Artists).Id);
        Assert.Equal("b1", track.Album?.Id);
    }

    [Fact]
    public async Task Searches_ClampLimitToAtLeastOne()
    {
        var transport = new StubTransport("""{"albums":{"items":[]}}""");
        var catalog = Build(transport, new CatalogOptions { Market = "JP", MaxSearchResults = 0 });

        Assert.Empty(await catalog.SearchAlbumsAsync("album", CancellationToken.None));

        Assert.Equal("/search?q=album&type=album&market=JP&limit=1", Assert.Single(transport.Urls));
    }

    [Fact]
    public async Task EmptyInput_DoesNotUseTransport()
    {
        var transport = new StubTransport();
        var catalog = Build(transport, new CatalogOptions());

        Assert.Empty(await catalog.SearchArtistsAsync("  ", CancellationToken.None));
        Assert.Null(await catalog.GetSongAsync(string.Empty, CancellationToken.None));
        Assert.Null(await catalog.GetAlbumAsync(" ", CancellationToken.None));
        Assert.Null(await catalog.GetArtistAsync("", CancellationToken.None));
        Assert.Empty(transport.Urls);
    }

    [Fact]
    public async Task InvalidJson_ReturnsEmptyOrNull()
    {
        var transport = new StubTransport("{", "not-json");
        var catalog = Build(transport, new CatalogOptions());

        Assert.Empty(await catalog.SearchArtistsAsync("artist", CancellationToken.None));
        Assert.Null(await catalog.GetArtistAsync("id", CancellationToken.None));
    }

    [Fact]
    public async Task GetAlbumAsync_FollowsAbsoluteTrackPagesAndMapsAllTracks()
    {
        var transport = new StubTransport(
            """{"id":"album/id","name":"Album","artists":[{"id":"artist","name":"Artist"}],"images":[],"copyrights":[{"text":"Copyright"}],"tracks":{"items":[{"id":"t1","name":"One"}],"next":"https://api.spotify.com/v1/albums/album%2Fid/tracks?offset=1"}}""",
            """{"items":[{"id":"t2","name":"Two"}],"next":null}""");
        var catalog = Build(transport, new CatalogOptions { Market = "JP" });

        var album = await catalog.GetAlbumAsync("album/id", CancellationToken.None);

        Assert.NotNull(album);
        Assert.Equal(["t1", "t2"], album.Tracks.Select(track => track.Id));
        Assert.Equal(
            ["/albums/album%2Fid?market=JP", "/albums/album%2Fid/tracks?offset=1"],
            transport.Urls);
    }

    [Fact]
    public async Task GetArtistAsync_DoesNotSendMarket()
    {
        var transport = new StubTransport("""{"id":"artist","name":"Name","genres":["genre"],"images":[]}""");
        var catalog = Build(transport, new CatalogOptions { Market = "JP" });

        var artist = await catalog.GetArtistAsync("artist", CancellationToken.None);

        Assert.Equal("Name", artist?.Name);
        Assert.Equal("/artists/artist", Assert.Single(transport.Urls));
    }

    [Fact]
    public async Task RateLimit_IsNotSwallowed()
    {
        var transport = new StubTransport(new CatalogRateLimitedException(TimeSpan.FromSeconds(3)));
        var catalog = Build(transport, new CatalogOptions());

        await Assert.ThrowsAsync<CatalogRateLimitedException>(
            () => catalog.SearchSongsAsync("song", CancellationToken.None));
    }

    [Theory]
    [InlineData(500, "large")]
    [InlineData(450, "large")]
    [InlineData(349, "small")]
    public void ArtworkSelector_ChoosesClosestAndPrefersLargerOnTie(int size, string expected)
    {
        IReadOnlyList<Image> images =
        [
            new Image { Url = "small", Width = 300, Height = 300 },
            new Image { Url = "large", Width = 600, Height = 600 },
        ];

        Assert.Equal(expected, ArtworkSelector.Select(images, size)?.Url);
    }

    private static SpotifyCatalog Build(StubTransport transport, CatalogOptions options)
        => new(transport, () => options, NullLogger<SpotifyCatalog>.Instance);

    private sealed class StubTransport : ICatalogTransport
    {
        private readonly Queue<object?> _responses;

        public StubTransport(params object?[] responses) => _responses = new Queue<object?>(responses);

        public List<string> Urls { get; } = [];

        public Task<string?> GetAsync(string relativeUrl, CancellationToken cancellationToken)
        {
            Urls.Add(relativeUrl);
            var response = _responses.Dequeue();
            if (response is Exception exception)
            {
                return Task.FromException<string?>(exception);
            }

            return Task.FromResult((string?)response);
        }
    }
}
