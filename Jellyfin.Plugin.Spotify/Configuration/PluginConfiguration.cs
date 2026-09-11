using System;
using Jellyfin.Plugin.Spotify.Catalog;
using Jellyfin.Plugin.Spotify.Catalog.Caching;
using Jellyfin.Plugin.Spotify.Catalog.Throttling;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Spotify.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        ClientId = string.Empty;
        ClientSecret = string.Empty;
        Market = "JP";
        MaxSearchResults = 25;
        ArtworkSize = 640;
        RequestTimeoutSeconds = 30;
        RequestIntervalMilliseconds = 1000;
        EnableCache = true;
        CacheLifetimeDays = 30;
        CacheNotFoundLifetimeHours = 24;
        MaxCacheMemoryMegabytes = 64;
        MaxPersistedEntryKilobytes = 8;
    }

    /// <summary>
    /// Gets or sets the client id of the Spotify application the plugin calls
    /// the Web API with.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret of that application.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the market to look items up in, as an ISO 3166-1 alpha-2
    /// country code. Spotify identifiers are global, so this only decides what
    /// is available and which relinked track an id resolves to.
    /// </summary>
    public string Market { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of search results requested per query.
    /// </summary>
    public int MaxSearchResults { get; set; }

    /// <summary>
    /// Gets or sets the edge length in pixels used when resolving Spotify
    /// artwork URL templates.
    /// </summary>
    public int ArtworkSize { get; set; }

    /// <summary>
    /// Gets or sets the per-request timeout in seconds for catalog calls.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the minimum time between two catalog requests, in
    /// milliseconds. Apple limits the search endpoint per IP address and keeps
    /// refusing for a long time once tripped, so requests are never sent in
    /// parallel and are spaced out by at least this much.
    /// </summary>
    public int RequestIntervalMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether catalog responses are cached
    /// locally so the same lookup is not fetched twice.
    /// </summary>
    public bool EnableCache { get; set; }

    /// <summary>
    /// Gets or sets how many days a cached response stays usable.
    /// </summary>
    public int CacheLifetimeDays { get; set; }

    /// <summary>
    /// Gets or sets how many hours a "not found" answer is remembered.
    /// </summary>
    public int CacheNotFoundLifetimeHours { get; set; }

    /// <summary>
    /// Gets or sets the memory budget for cached responses, in megabytes.
    /// </summary>
    public int MaxCacheMemoryMegabytes { get; set; }

    /// <summary>
    /// Gets or sets the largest response written to disk, in kilobytes.
    /// Larger ones are kept in memory only.
    /// </summary>
    public int MaxPersistedEntryKilobytes { get; set; }

    /// <summary>
    /// Projects this configuration onto the Jellyfin-independent options used by
    /// the catalog layer.
    /// </summary>
    /// <returns>Catalog options.</returns>
    public CatalogOptions ToCatalogOptions()
    {
        return new CatalogOptions
        {
            Market = Market,
            MaxSearchResults = MaxSearchResults,
            ArtworkSize = ArtworkSize,
        };
    }

    /// <summary>
    /// Projects the request pacing settings onto the options used by the throttle.
    /// </summary>
    /// <returns>Throttle options.</returns>
    public ThrottleOptions ToThrottleOptions()
    {
        return new ThrottleOptions
        {
            MinInterval = TimeSpan.FromMilliseconds(Math.Max(0, RequestIntervalMilliseconds)),
        };
    }

    /// <summary>
    /// Projects the cache settings onto the options used by the cache itself.
    /// </summary>
    /// <returns>Cache options.</returns>
    public CatalogCacheOptions ToCacheOptions()
    {
        return new CatalogCacheOptions
        {
            Enabled = EnableCache,
            Lifetime = TimeSpan.FromDays(Math.Max(1, CacheLifetimeDays)),
            NegativeLifetime = TimeSpan.FromHours(Math.Max(1, CacheNotFoundLifetimeHours)),
            MaxMemoryBytes = Math.Max(1, MaxCacheMemoryMegabytes) * 1024L * 1024L,
            MaxPersistedEntryBytes = Math.Max(0, MaxPersistedEntryKilobytes) * 1024,
        };
    }
}
