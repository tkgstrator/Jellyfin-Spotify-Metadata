using Jellyfin.Plugin.Spotify.Configuration;
using Xunit;

namespace Jellyfin.Plugin.Spotify.Tests;

public class PluginConfigurationTests
{
    [Fact]
    public void Defaults_AreUsableWithoutTouchingTheSettingsPage()
    {
        var config = new PluginConfiguration();

        Assert.Equal("JP", config.Market);
        Assert.True(config.EnableCache);
        Assert.True(config.MaxSearchResults is > 0 and <= 50, "Spotify caps a search at 50 results.");
        Assert.True(config.RequestIntervalMilliseconds > 0);
    }

    [Fact]
    public void Credentials_StartEmptySoNothingIsShippedWithThePlugin()
    {
        var config = new PluginConfiguration();

        Assert.Empty(config.ClientId);
        Assert.Empty(config.ClientSecret);
    }

    [Fact]
    public void ToCatalogOptions_CarriesTheMarketAndLimits()
    {
        var config = new PluginConfiguration { Market = "US", MaxSearchResults = 10, ArtworkSize = 300 };

        var options = config.ToCatalogOptions();

        Assert.Equal("US", options.Market);
        Assert.Equal(10, options.MaxSearchResults);
        Assert.Equal(300, options.ArtworkSize);
    }

    [Fact]
    public void ToThrottleOptions_CarriesTheRequestInterval()
    {
        var config = new PluginConfiguration { RequestIntervalMilliseconds = 250 };

        Assert.Equal(250, config.ToThrottleOptions().MinInterval.TotalMilliseconds);
    }
}
