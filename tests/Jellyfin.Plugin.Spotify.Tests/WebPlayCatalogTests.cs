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
    public async Task IdLookupReturnsOnlyExactSearchMatch()
    {
        var transport = new FixtureTransport(
            """{"data":{"searchV2":{"artists":{"items":[{"data":{"uri":"spotify:artist:other","profile":{"name":"Other"}}},{"data":{"uri":"spotify:artist:wanted","profile":{"name":"Wanted"}}}],"pagingInfo":{}}}}}""");
        var catalog = CreateCatalog(transport, 25);

        var result = await catalog.GetArtistAsync("wanted", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("wanted", result.Id);
        Assert.Equal("wanted", transport.Terms.Single());
        Assert.Equal("searchArtists", transport.Operations.Single());
    }

    private static WebPlayCatalog CreateCatalog(IWebPlayTransport transport, int maximum)
        => new(transport, () => new CatalogOptions { MaxSearchResults = maximum }, NullLogger<WebPlayCatalog>.Instance);

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
