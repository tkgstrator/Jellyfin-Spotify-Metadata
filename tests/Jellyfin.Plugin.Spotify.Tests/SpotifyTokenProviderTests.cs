using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Catalog;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jellyfin.Plugin.Spotify.Tests;

public class SpotifyTokenProviderTests
{
    [Fact]
    public void ParseToken_ReadsTheTokenAndItsLifetime()
    {
        var (token, lifetime) = SpotifyTokenProvider.ParseToken(
            """{"access_token":"abc","token_type":"Bearer","expires_in":3600}""");

        Assert.Equal("abc", token);
        Assert.Equal(TimeSpan.FromHours(1), lifetime);
    }

    [Fact]
    public void ParseToken_FallsBackToAnHourWhenTheLifetimeIsMissing()
        => Assert.Equal(TimeSpan.FromHours(1), SpotifyTokenProvider.ParseToken("""{"access_token":"abc"}""").Lifetime);

    [Theory]
    [InlineData("""{"error":"invalid_client"}""")]
    [InlineData("not json at all")]
    public void ParseToken_RejectsABodyWithoutAToken(string body)
        => Assert.Throws<CatalogAuthenticationException>(() => SpotifyTokenProvider.ParseToken(body));

    [Fact]
    public async Task GetTokenAsync_FetchesOnceAndReusesUntilTheTokenIsNearlyExpired()
    {
        var handler = new StubHandler();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var provider = Build(handler, time);

        Assert.Equal("token-1", await provider.GetTokenAsync(CancellationToken.None));
        Assert.Equal("token-1", await provider.GetTokenAsync(CancellationToken.None));
        Assert.Equal(1, handler.Calls);

        // Still inside the hour, but within the minute of margin: refreshed
        // early so a request in flight cannot cross the expiry.
        time.Advance(TimeSpan.FromMinutes(59.5));
        Assert.Equal("token-2", await provider.GetTokenAsync(CancellationToken.None));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task GetTokenAsync_FetchesAgainAfterInvalidate()
    {
        var handler = new StubHandler();
        var provider = Build(handler, new FakeTimeProvider(DateTimeOffset.UtcNow));

        Assert.Equal("token-1", await provider.GetTokenAsync(CancellationToken.None));
        provider.Invalidate();

        Assert.Equal("token-2", await provider.GetTokenAsync(CancellationToken.None));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task GetTokenAsync_AsksOnceWhenSeveralLookupsRaceForTheFirstToken()
    {
        var handler = new StubHandler { Delay = TimeSpan.FromMilliseconds(30) };
        var provider = Build(handler, new FakeTimeProvider(DateTimeOffset.UtcNow));

        var tokens = await Task.WhenAll(
            provider.GetTokenAsync(CancellationToken.None),
            provider.GetTokenAsync(CancellationToken.None),
            provider.GetTokenAsync(CancellationToken.None));

        Assert.All(tokens, t => Assert.Equal("token-1", t));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GetTokenAsync_SaysSoWhenNoCredentialsAreConfigured()
    {
        var provider = new SpotifyTokenProvider(
            new HttpClient(new StubHandler()),
            () => new SpotifyCredentials(string.Empty, string.Empty),
            NullLogger<SpotifyTokenProvider>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenAsync_SurfacesARefusalAsAnAuthenticationFailure()
    {
        var handler = new StubHandler { Status = HttpStatusCode.BadRequest };
        var provider = Build(handler, new FakeTimeProvider(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<CatalogAuthenticationException>(() => provider.GetTokenAsync(CancellationToken.None));
    }

    private static SpotifyTokenProvider Build(StubHandler handler, TimeProvider time)
        => new(
            new HttpClient(handler),
            () => new SpotifyCredentials("id", "secret"),
            NullLogger<SpotifyTokenProvider>.Instance,
            time);

    private sealed class StubHandler : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        public TimeSpan Delay { get; init; }

        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var n = Interlocked.Increment(ref _calls);
            lock (Requests)
            {
                Requests.Add(request.RequestUri!.ToString());
            }

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            var body = Status == HttpStatusCode.OK
                ? string.Format(CultureInfo.InvariantCulture, """{{"access_token":"token-{0}","expires_in":3600}}""", n)
                : """{"error":"invalid_client"}""";
            return new HttpResponseMessage(Status) { Content = new StringContent(body) };
        }
    }
}
