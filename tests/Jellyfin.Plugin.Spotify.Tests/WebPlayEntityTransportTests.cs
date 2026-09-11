using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Catalog.WebPlay;
using Xunit;

namespace Jellyfin.Plugin.Spotify.Tests;

public class WebPlayEntityTransportTests
{
    [Fact]
    public async Task GetDecodesTheExactInitialStateScript()
    {
        const string json = "{\"entities\":{\"items\":{}}}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var transport = Create($"<html><script id=\"initialState\" type=\"text/plain\">{encoded}</script></html>");

        var result = await transport.GetAsync("track", "id", CancellationToken.None);

        Assert.Equal(json, result);
    }

    [Fact]
    public async Task GetReturnsNullOnlyForNotFound()
    {
        var transport = new WebPlayEntityTransport(new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))));

        Assert.Null(await transport.GetAsync("album", "missing", CancellationToken.None));
    }

    [Theory]
    [InlineData("<script type=\"text/plain\" id=\"initialState\">e30=</script>")]
    [InlineData("<script id=\"initialState\" type=\"text/plain\">not base64</script>")]
    [InlineData("<script id=\"initialState\" type=\"text/plain\">e30=</script><script id=\"initialState\" type=\"text/plain\">e30=</script>")]
    public async Task GetRejectsProtocolDrift(string html)
    {
        var transport = Create(html);

        await Assert.ThrowsAsync<WebPlayEntityProtocolException>(() => transport.GetAsync("artist", "id", CancellationToken.None));
    }

    [Fact]
    public async Task GetRejectsOversizedResponse()
    {
        var transport = Create(new string('x', WebPlayEntityTransport.MaximumHtmlBytes + 1));

        await Assert.ThrowsAsync<WebPlayEntityProtocolException>(() => transport.GetAsync("track", "id", CancellationToken.None));
    }

    private static WebPlayEntityTransport Create(string html)
        => new(new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html),
        })));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }
}
