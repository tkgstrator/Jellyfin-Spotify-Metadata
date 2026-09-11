using System.Collections.Generic;

namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// A catalog resource.
/// </summary>
/// <typeparam name="TAttributes">Attribute payload type.</typeparam>
public class CatalogItem<TAttributes>
    where TAttributes : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogItem{TAttributes}"/> class.
    /// </summary>
    /// <param name="id">Spotify catalog identifier.</param>
    /// <param name="attributes">Attribute payload.</param>
    public CatalogItem(string id, TAttributes attributes)
    {
        Id = id;
        Attributes = attributes;
    }

    /// <summary>
    /// Gets the Spotify catalog identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the attribute payload.
    /// </summary>
    public TAttributes Attributes { get; }

    /// <summary>
    /// Gets the ids of the related artists. Populated by id lookups only.
    /// </summary>
    public IReadOnlyList<string> ArtistIds { get; init; } = [];

    /// <summary>
    /// Gets the ids of the related albums. Populated by song id lookups only.
    /// </summary>
    public IReadOnlyList<string> AlbumIds { get; init; } = [];

    /// <summary>
    /// Gets the ids of the album's tracks, in order. Populated by album id
    /// lookups only.
    /// </summary>
    /// <remarks>
    /// Ids rather than whole items: what an Spotify track response looks
    /// like is not settled yet, and the identifiers are enough to resolve a
    /// track without searching.
    /// </remarks>
    public IReadOnlyList<string> TrackIds { get; init; } = [];
}
