using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Catalog;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Spotify.Tests;

public class SpotifyTransportTests
{
    [Fact]
    public async Task GetAsync_SendsTheBearerTokenAndReturnsTheRawBody()
    {
        var handler = new StubHandler(_ => Ok("""{"id":"abc"}"""));
        var transport = Build(handler, out var tokens);

        var body = await transport.GetAsync("/albums/abc", CancellationToken.None);

        Assert.Equal("""{"id":"abc"}""", body);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.spotify.com/v1/albums/abc", request.Uri);
        Assert.Equal("Bearer token", request.Authorization);
        Assert.Equal(1, tokens.Handed);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForNotFound()
    {
        var transport = Build(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)), out _);

        Assert.Null(await transport.GetAsync("/albums/missing", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_RefreshesTheTokenOnceWhenItIsRejected()
    {
        var handler = new StubHandler(n => n == 1
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : Ok("""{"id":"abc"}"""));
        var transport = Build(handler, out var tokens);

        var body = await transport.GetAsync("/albums/abc", CancellationToken.None);

        Assert.Equal("""{"id":"abc"}""", body);
        Assert.Equal(1, tokens.Invalidated);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetAsync_GivesUpWhenTheSecondTokenIsAlsoRejected()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var transport = Build(handler, out _);

        await Assert.ThrowsAsync<CatalogAuthenticationException>(
            () => transport.GetAsync("/albums/abc", CancellationToken.None));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetAsync_CarriesTheRetryAfterSpotifyAsksFor()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.TryAddWithoutValidation("Retry-After", "17");
            return response;
        });
        var transport = Build(handler, out _);

        var ex = await Assert.ThrowsAsync<CatalogRateLimitedException>(
            () => transport.GetAsync("/search?q=x", CancellationToken.None));

        Assert.Equal(TimeSpan.FromSeconds(17), ex.RetryAfter);
    }

    [Fact]
    public async Task GetAsync_StillReportsRateLimitingWithoutTheHeader()
    {
        var transport = Build(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)), out _);

        var ex = await Assert.ThrowsAsync<CatalogRateLimitedException>(
            () => transport.GetAsync("/search?q=x", CancellationToken.None));

        Assert.Null(ex.RetryAfter);
    }

    private static HttpResponseMessage Ok(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static SpotifyTransport Build(StubHandler handler, out StubTokens tokens)
    {
        tokens = new StubTokens();
        return new SpotifyTransport(new HttpClient(handler), tokens, NullLogger<SpotifyTransport>.Instance);
    }

    private sealed class StubTokens : ISpotifyTokenProvider
    {
        public int Handed { get; private set; }

        public int Invalidated { get; private set; }

        public Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            Handed++;
            return Task.FromResult("token");
        }

        public void Invalidate() => Invalidated++;
    }

    private sealed record Sent(string Uri, string? Authorization);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpResponseMessage> _responder;

        public StubHandler(Func<int, HttpResponseMessage> responder) => _responder = responder;

        public List<Sent> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new Sent(
                request.RequestUri!.ToString(),
                request.Headers.TryGetValues("Authorization", out var values) ? values.First() : null));
            return Task.FromResult(_responder(Requests.Count));
        }
    }
}
