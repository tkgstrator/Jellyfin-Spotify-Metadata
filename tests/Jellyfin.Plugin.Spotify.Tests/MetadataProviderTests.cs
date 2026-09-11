using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Catalog;
using Jellyfin.Plugin.Spotify.Catalog.Models;
using Jellyfin.Plugin.Spotify.ExternalIds;
using Jellyfin.Plugin.Spotify.Providers;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Spotify.Tests;

public class MetadataProviderTests
{
    [Fact]
    public async Task SongProvider_PrefersStoredIdAndMapsTrack()
    {
        var catalog = new FakeCatalog
        {
            Tracks =
            [
                new Track
                {
                    Id = "track-id",
                    Name = "Song",
                    TrackNumber = 3,
                    DiscNumber = 2,
                    DurationMs = 123456,
                    Artists = [new ArtistRef { Id = "artist-id", Name = "Artist" }],
                    Album = new AlbumRef
                    {
                        Id = "album-id",
                        Name = "Album",
                        ReleaseDate = "2024-05",
                        Images = [new Image { Url = "https://image/640", Width = 640, Height = 640 }],
                    },
                },
            ],
        };
        var provider = CreateSongProvider(catalog);
        var info = new SongInfo { Name = "Wrong" };
        info.SetProviderId(ProviderKeys.Song, "track-id");

        var result = await provider.GetMetadata(info, CancellationToken.None);

        Assert.True(result.HasMetadata);
        Assert.Equal("Song", result.Item.Name);
        Assert.Equal("Album", result.Item.Album);
        Assert.Equal(["Artist"], result.Item.Artists);
        Assert.Equal(["Artist"], result.Item.AlbumArtists);
        Assert.Equal(3, result.Item.IndexNumber);
        Assert.Equal(2, result.Item.ParentIndexNumber);
        Assert.Equal(TimeSpan.FromMilliseconds(123456).Ticks, result.Item.RunTimeTicks);
        Assert.Equal(new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc), result.Item.PremiereDate);
        Assert.Equal("track-id", result.Item.GetProviderId(ProviderKeys.Song));
        Assert.Equal("album-id", result.Item.GetProviderId(ProviderKeys.Album));
        Assert.Equal(["track-id"], catalog.TrackLookups);
        Assert.Empty(catalog.Searches);
    }

    [Fact]
    public async Task SongProvider_SearchesWithArtistAlbumAndTitleAndUsesAlbumImage()
    {
        var catalog = new FakeCatalog
        {
            Tracks =
            [
                new Track
                {
                    Id = "track-id",
                    Name = "Title",
                    Album = new AlbumRef
                    {
                        Id = "album-id",
                        Images =
                        [
                            new Image { Url = "https://image/300", Width = 300, Height = 300 },
                            new Image { Url = "https://image/640", Width = 640, Height = 640 },
                        ],
                    },
                },
            ],
        };
        var provider = CreateSongProvider(catalog);

        var results = (await provider.GetSearchResults(
            new SongInfo { Name = "Title", Album = "Album", Artists = ["Artist"] },
            CancellationToken.None)).ToList();

        Assert.Equal(["Artist Album Title"], catalog.Searches);
        Assert.Equal("https://image/640", Assert.Single(results).ImageUrl);
    }

    [Fact]
    public async Task AlbumProvider_MapsArtistsDatePrecisionAndLabel()
    {
        var catalog = new FakeCatalog
        {
            Albums =
            [
                new Album
                {
                    Id = "album-id",
                    Name = "Album",
                    ReleaseDate = "1999",
                    Label = "Label",
                    Artists = [new ArtistRef { Id = "artist-id", Name = "Artist" }],
                },
            ],
        };
        var provider = CreateAlbumProvider(catalog);

        var result = await provider.GetMetadata(
            new AlbumInfo { Name = "Album", AlbumArtists = ["Artist"] },
            CancellationToken.None);

        Assert.True(result.HasMetadata);
        Assert.Equal(["Artist"], result.Item.AlbumArtists);
        Assert.Equal(["Artist"], result.Item.Artists);
        Assert.Equal(new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc), result.Item.PremiereDate);
        Assert.Equal(1999, result.Item.ProductionYear);
        Assert.Equal(["Label"], result.Item.Studios);
        Assert.Equal("album-id", result.Item.GetProviderId(ProviderKeys.Album));
        Assert.Equal(["Artist Album"], catalog.Searches);
    }

    [Theory]
    [InlineData("2020", 2020, 1, 1)]
    [InlineData("2020-03", 2020, 3, 1)]
    [InlineData("2020-03-17", 2020, 3, 17)]
    [InlineData("invalid", 0, 0, 0)]
    public void ParseReleaseDate_RespectsSpotifyPrecision(string value, int year, int month, int day)
    {
        var parsed = AlbumMetadataProvider.ParseReleaseDate(value);
        if (year == 0)
        {
            Assert.Null(parsed);
        }
        else
        {
            Assert.Equal(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc), parsed);
        }
    }

    [Fact]
    public async Task ArtistProvider_PrefersStoredIdAndOnlyMapsAvailableFields()
    {
        var catalog = new FakeCatalog
        {
            Artists = [new Artist { Id = "artist-id", Name = "Artist", Genres = ["rock", "pop"] }],
        };
        var provider = CreateArtistProvider(catalog);
        var info = new ArtistInfo { Name = "Wrong" };
        info.SetProviderId(ProviderKeys.Artist, "artist-id");

        var result = await provider.GetMetadata(info, CancellationToken.None);

        Assert.True(result.HasMetadata);
        Assert.Equal("Artist", result.Item.Name);
        Assert.Equal(["rock", "pop"], result.Item.Genres);
        Assert.Null(result.Item.Overview);
        Assert.Empty(result.Item.ProductionLocations);
        Assert.Equal(["artist-id"], catalog.ArtistLookups);
        Assert.Empty(catalog.Searches);
    }

    [Fact]
    public async Task Providers_ReportNoMetadataForEmptyResults()
    {
        var catalog = new FakeCatalog();

        Assert.False((await CreateSongProvider(catalog).GetMetadata(new SongInfo { Name = "x" }, CancellationToken.None)).HasMetadata);
        Assert.False((await CreateAlbumProvider(catalog).GetMetadata(new AlbumInfo { Name = "x" }, CancellationToken.None)).HasMetadata);
        Assert.False((await CreateArtistProvider(catalog).GetMetadata(new ArtistInfo { Name = "x" }, CancellationToken.None)).HasMetadata);
        Assert.Equal(["x", "x", "x"], catalog.Searches);
    }

    [Fact]
    public async Task ImageProviders_PreferIdsSelectClosestArtworkAndFetchViaFactory()
    {
        var catalog = new FakeCatalog
        {
            Albums =
            [
                new Album
                {
                    Id = "album-id",
                    Images =
                    [
                        new Image { Url = "https://image/300", Width = 300, Height = 300 },
                        new Image { Url = "https://image/640", Width = 640, Height = 640 },
                    ],
                },
            ],
            Artists =
            [
                new Artist
                {
                    Id = "artist-id",
                    Images = [new Image { Url = "https://image/artist", Width = 500, Height = 500 }],
                },
            ],
        };
        var factory = new StubHttpClientFactory();
        var albumProvider = new AlbumImageProvider(catalog, factory, NullLogger<AlbumImageProvider>.Instance);
        var artistProvider = new ArtistImageProvider(catalog, factory, NullLogger<ArtistImageProvider>.Instance);
        var album = new MusicAlbum();
        album.SetProviderId(ProviderKeys.Album, "album-id");
        var artist = new MusicArtist();
        artist.SetProviderId(ProviderKeys.Artist, "artist-id");

        var albumImage = Assert.Single(await albumProvider.GetImages(album, CancellationToken.None));
        var artistImage = Assert.Single(await artistProvider.GetImages(artist, CancellationToken.None));
        await albumProvider.GetImageResponse(albumImage.Url, CancellationToken.None);

        Assert.Equal("https://image/640", albumImage.Url);
        Assert.Equal(640, albumImage.Width);
        Assert.Equal("https://image/artist", artistImage.Url);
        Assert.Equal(["album-id"], catalog.AlbumLookups);
        Assert.Equal(["artist-id"], catalog.ArtistLookups);
        Assert.Equal(new Uri("https://image/640"), factory.Handler.LastRequestUri);
        Assert.Empty(catalog.Searches);
    }

    [Fact]
    public async Task ImageProviders_SearchWhenIdIsMissing()
    {
        var catalog = new FakeCatalog
        {
            Albums = [new Album { Images = [new Image { Url = "https://album", Width = 640, Height = 640 }] }],
            Artists = [new Artist { Images = [new Image { Url = "https://artist", Width = 640, Height = 640 }] }],
        };
        var factory = new StubHttpClientFactory();
        var albumProvider = new AlbumImageProvider(catalog, factory, NullLogger<AlbumImageProvider>.Instance);
        var artistProvider = new ArtistImageProvider(catalog, factory, NullLogger<ArtistImageProvider>.Instance);
        var album = new MusicAlbum { Name = "Album", AlbumArtists = ["Artist"] };
        var artist = new MusicArtist { Name = "Artist" };

        Assert.Single(await albumProvider.GetImages(album, CancellationToken.None));
        Assert.Single(await artistProvider.GetImages(artist, CancellationToken.None));

        Assert.Equal(["Artist Album", "Artist"], catalog.Searches);
    }

    private static SongMetadataProvider CreateSongProvider(FakeCatalog catalog)
        => new(catalog, new StubHttpClientFactory(), NullLogger<SongMetadataProvider>.Instance);

    private static AlbumMetadataProvider CreateAlbumProvider(FakeCatalog catalog)
        => new(catalog, new StubHttpClientFactory(), NullLogger<AlbumMetadataProvider>.Instance);

    private static ArtistMetadataProvider CreateArtistProvider(FakeCatalog catalog)
        => new(catalog, new StubHttpClientFactory(), NullLogger<ArtistMetadataProvider>.Instance);

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public RecordingHandler Handler { get; } = new();

        public HttpClient CreateClient(string name) => new(Handler, disposeHandler: false);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class FakeCatalog : ISpotifyCatalog
    {
        public IReadOnlyList<Track> Tracks { get; init; } = [];

        public IReadOnlyList<Album> Albums { get; init; } = [];

        public IReadOnlyList<Artist> Artists { get; init; } = [];

        public List<string> Searches { get; } = [];

        public List<string> TrackLookups { get; } = [];

        public List<string> AlbumLookups { get; } = [];

        public List<string> ArtistLookups { get; } = [];

        public Task<IReadOnlyList<Track>> SearchSongsAsync(string term, CancellationToken cancellationToken)
        {
            Searches.Add(term);
            return Task.FromResult(Tracks);
        }

        public Task<IReadOnlyList<Album>> SearchAlbumsAsync(string term, CancellationToken cancellationToken)
        {
            Searches.Add(term);
            return Task.FromResult(Albums);
        }

        public Task<IReadOnlyList<Artist>> SearchArtistsAsync(string term, CancellationToken cancellationToken)
        {
            Searches.Add(term);
            return Task.FromResult(Artists);
        }

        public Task<Track?> GetSongAsync(string id, CancellationToken cancellationToken)
        {
            TrackLookups.Add(id);
            return Task.FromResult(Tracks.FirstOrDefault(item => item.Id == id));
        }

        public Task<Album?> GetAlbumAsync(string id, CancellationToken cancellationToken)
        {
            AlbumLookups.Add(id);
            return Task.FromResult(Albums.FirstOrDefault(item => item.Id == id));
        }

        public Task<Artist?> GetArtistAsync(string id, CancellationToken cancellationToken)
        {
            ArtistLookups.Add(id);
            return Task.FromResult(Artists.FirstOrDefault(item => item.Id == id));
        }
    }
}
