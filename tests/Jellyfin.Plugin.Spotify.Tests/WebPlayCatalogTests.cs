using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Catalog;
using Jellyfin.Plugin.Spotify.Catalog.WebPlay;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Spotify.Tests;

public class WebPlayCatalogTests
{
    [Fact]
    public async Task SearchSongsReadsUriAndNextOffset()
    {
        var transport = new FixtureTransport(
            """{"data":{"searchV2":{"tracksV2":{"items":[{"item":{"data":{"uri":"spotify:track:first","name":"First","trackNumber":1,"discNumber":1,"duration":{"totalMilliseconds":1234},"artists":{"items":[{"uri":"spotify:artist:a","profile":{"name":"Artist"}}]}}}}],"pagingInfo":{"nextOffset":1}}}}}""",
            """{"data":{"searchV2":{"tracksV2":{"items":[{"item":{"data":{"uri":"spotify:track:second","name":"Second"}}}],"pagingInfo":{"nextOffset":null}}}}}""");
        var catalog = CreateCatalog(transport, 2);

        var results = await catalog.SearchSongsAsync("query", CancellationToken.None);

        Assert.Equal(["first", "second"], results.Select(item => item.Id));
        Assert.Equal([0, 1], transport.Offsets);
        Assert.All(transport.Hashes, hash => Assert.Equal(WebPlayCatalog.SearchTracksHash, hash));
    }

    [Fact]
    public async Task IdLookupUsesEntityTransportWithoutSearching()
    {
        var searchTransport = new FixtureTransport();
        var entityTransport = new FixtureEntityTransport(
            """{"entities":{"items":{"spotify:artist:wanted":{"uri":"spotify:artist:wanted","profile":{"name":"Wanted"},"visuals":{"avatarImage":{"sources":[{"url":"https://example.com/artist.jpg","width":640,"height":640}]}}}}}}""");
        var catalog = CreateCatalog(searchTransport, 25, entityTransport);

        var result = await catalog.GetArtistAsync("wanted", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("wanted", result.Id);
        Assert.Equal("Wanted", result.Name);
        Assert.Empty(searchTransport.Operations);
        Assert.Equal([("artist", "wanted")], entityTransport.Requests);
    }

    private static WebPlayCatalog CreateCatalog(
        IWebPlayTransport transport,
        int maximum,
        IWebPlayEntityTransport? entityTransport = null)
        => new(
            transport,
            entityTransport ?? new FixtureEntityTransport(null),
            () => new CatalogOptions { MaxSearchResults = maximum },
            NullLogger<WebPlayCatalog>.Instance);

    private sealed class FixtureEntityTransport(string? response) : IWebPlayEntityTransport
    {
        public List<(string EntityType, string Id)> Requests { get; } = [];

        public Task<string?> GetAsync(string entityType, string id, CancellationToken cancellationToken)
        {
            Requests.Add((entityType, id));
            return Task.FromResult(response);
        }
    }

    private sealed class FixtureTransport(params string[] responses) : IWebPlayTransport
    {
        private int _index;

        public List<int> Offsets { get; } = [];

        public List<string> Terms { get; } = [];

        public List<string> Operations { get; } = [];

        public List<string> Hashes { get; } = [];

        public Task<string> PostAsync(string operationName, string sha256Hash, JsonElement variables, CancellationToken cancellationToken)
        {
            Operations.Add(operationName);
            Hashes.Add(sha256Hash);
            Offsets.Add(variables.GetProperty("offset").GetInt32());
            Terms.Add(variables.GetProperty("searchTerm").GetString()!);
            return Task.FromResult(responses[_index++]);
        }
    }
}
