using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Catalog.WebPlay;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Spotify.Tests;

public class WebPlayTransportTests
{
    [Fact]
    public async Task PostAddsWebPlayerHeadersAndPersistedQuery()
    {
        HttpRequestMessage? captured = null;
        string? content = null;
        var handler = new StubHandler(async request =>
        {
            captured = request;
            content = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        var transport = new WebPlayTransport(
            new HttpClient(handler),
            new SessionProvider(new WebPlaySession("token", "dynamic-version")),
            NullLogger<WebPlayTransport>.Instance);

        await transport.PostAsync("searchTracks", "hash", JsonSerializer.SerializeToElement(new { offset = 7 }), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured.Headers.Authorization?.Scheme);
        Assert.Equal("token", captured.Headers.Authorization?.Parameter);
        Assert.Equal("WebPlayer", captured.Headers.GetValues("App-Platform").Single());
        Assert.Equal("dynamic-version", captured.Headers.GetValues("Spotify-App-Version").Single());
        using var document = JsonDocument.Parse(content!);
        Assert.Equal("hash", document.RootElement.GetProperty("extensions").GetProperty("persistedQuery").GetProperty("sha256Hash").GetString());
    }

    private sealed class SessionProvider(WebPlaySession session) : IWebPlaySessionProvider
    {
        public Task<WebPlaySession> GetSessionAsync(CancellationToken cancellationToken) => Task.FromResult(session);

        public void Invalidate()
        {
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
