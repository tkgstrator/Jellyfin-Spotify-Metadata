using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Catalog.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Spotify.Catalog.WebPlay;

/// <summary>
/// Reads Spotify catalog data through unsupported public Web Player endpoints.
/// </summary>
/// <remarks>
/// Search uses the GraphQL endpoint. Identifier lookup reads the public entity
/// page's embedded initial-state JSON.
/// </remarks>
public sealed class WebPlayCatalog : ISpotifyCatalog
{
    /// <summary>
    /// Persisted hash observed for <c>searchTracks</c> on 2026-09-11.
    /// </summary>
    public const string SearchTracksHash = "59ee4a659c32e9ad894a71308207594a65ba67bb6b632b183abe97303a51fa55";

    /// <summary>
    /// Persisted hash observed for <c>searchAlbums</c> on 2026-09-11.
    /// </summary>
    public const string SearchAlbumsHash = "64ae1fe6df380b038c0a65a2606d3361bc270de6870b2fdc99cf0848b1efa6d3";

    /// <summary>
    /// Persisted hash observed for <c>searchArtists</c> on 2026-09-11.
    /// </summary>
    public const string SearchArtistsHash = "270905851ba5c7faca81cfe053c2dbd8ceb4f156a0e0ef4b385af75ab69ffd13";

    private readonly IWebPlayTransport _transport;
    private readonly IWebPlayEntityTransport _entityTransport;
    private readonly Func<CatalogOptions> _options;
    private readonly ILogger<WebPlayCatalog> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebPlayCatalog"/> class.
    /// </summary>
    /// <param name="transport">GraphQL search transport.</param>
    /// <param name="entityTransport">Public entity-page transport.</param>
    /// <param name="options">Supplies current catalog options.</param>
    /// <param name="logger">Logger.</param>
    public WebPlayCatalog(
        IWebPlayTransport transport,
        IWebPlayEntityTransport entityTransport,
        Func<CatalogOptions> options,
        ILogger<WebPlayCatalog> logger)
    {
        _transport = transport;
        _entityTransport = entityTransport;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Track>> SearchSongsAsync(string term, CancellationToken cancellationToken)
        => SearchAsync(term, "searchTracks", SearchTracksHash, WebPlaySearchAdapter.ReadTracks, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Album>> SearchAlbumsAsync(string term, CancellationToken cancellationToken)
        => SearchAsync(term, "searchAlbums", SearchAlbumsHash, WebPlaySearchAdapter.ReadAlbums, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Artist>> SearchArtistsAsync(string term, CancellationToken cancellationToken)
        => SearchAsync(term, "searchArtists", SearchArtistsHash, WebPlaySearchAdapter.ReadArtists, cancellationToken);

    /// <inheritdoc />
    public Task<Track?> GetSongAsync(string id, CancellationToken cancellationToken)
        => GetEntityAsync(id, "track", WebPlayEntityAdapter.ReadTrack, cancellationToken);

    /// <inheritdoc />
    public Task<Album?> GetAlbumAsync(string id, CancellationToken cancellationToken)
        => GetEntityAsync(id, "album", WebPlayEntityAdapter.ReadAlbum, cancellationToken);

    /// <inheritdoc />
    public Task<Artist?> GetArtistAsync(string id, CancellationToken cancellationToken)
        => GetEntityAsync(id, "artist", WebPlayEntityAdapter.ReadArtist, cancellationToken);

    private async Task<T?> GetEntityAsync<T>(
        string id,
        string entityType,
        Func<string, string, T> read,
        CancellationToken cancellationToken)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var json = await _entityTransport.GetAsync(entityType, id, cancellationToken).ConfigureAwait(false);
        return json is null ? null : read(json, id);
    }

    private async Task<IReadOnlyList<T>> SearchAsync<T>(
        string term,
        string operation,
        string hash,
        Func<string, WebPlayPage<T>> read,
        CancellationToken cancellationToken)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        var maximum = Math.Clamp(_options().MaxSearchResults, 1, 50);
        var results = new List<T>(maximum);
        var offset = 0;
        while (results.Count < maximum)
        {
            var limit = maximum - results.Count;
            var variables = JsonSerializer.SerializeToElement(
                new
                {
                    searchTerm = term,
                    offset,
                    limit,
                },
                CatalogJson.Options);
            var json = await _transport.PostAsync(operation, hash, variables, cancellationToken).ConfigureAwait(false);
            WebPlayPage<T> page;
            try
            {
                page = read(json);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Could not parse WebPlay operation {OperationName}", operation);
                return results;
            }

            results.AddRange(page.Items.Take(maximum - results.Count));
            if (page.NextOffset is not { } nextOffset || nextOffset <= offset || page.Items.Count == 0)
            {
                break;
            }

            offset = nextOffset;
        }

        return results;
    }
}
