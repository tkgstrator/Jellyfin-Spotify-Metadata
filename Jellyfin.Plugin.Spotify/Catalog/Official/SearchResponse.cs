using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Spotify.Catalog.Official;

#pragma warning disable SA1402 // The private wire DTOs form one response schema.

internal sealed class SearchResponse
{
    public Page<TrackObject>? Tracks { get; init; }

    public Page<AlbumObject>? Albums { get; init; }

    public Page<ArtistObject>? Artists { get; init; }
}

internal sealed class Page<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public string? Next { get; init; }
}

internal sealed class ImageObject
{
    public string Url { get; init; } = string.Empty;

    public int Width { get; init; }

    public int Height { get; init; }
}

internal sealed class ArtistReferenceObject
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}

internal sealed class AlbumReferenceObject
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; init; }

    public IReadOnlyList<ImageObject> Images { get; init; } = [];
}

internal sealed class ExternalIdsObject
{
    public string? Isrc { get; init; }
}

internal sealed class TrackObject
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("track_number")]
    public int TrackNumber { get; init; }

    [JsonPropertyName("disc_number")]
    public int DiscNumber { get; init; }

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; init; }

    public bool Explicit { get; init; }

    public IReadOnlyList<ArtistReferenceObject> Artists { get; init; } = [];

    public AlbumReferenceObject? Album { get; init; }

    [JsonPropertyName("external_ids")]
    public ExternalIdsObject? ExternalIds { get; init; }
}

internal sealed class CopyrightObject
{
    public string Text { get; init; } = string.Empty;
}

internal sealed class AlbumObject
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("album_type")]
    public string? AlbumType { get; init; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; init; }

    public string? Label { get; init; }

    public IReadOnlyList<CopyrightObject> Copyrights { get; init; } = [];

    public IReadOnlyList<ArtistReferenceObject> Artists { get; init; } = [];

    public IReadOnlyList<ImageObject> Images { get; init; } = [];

    public Page<TrackObject>? Tracks { get; init; }
}

internal sealed class FollowersObject
{
    public int Total { get; init; }
}

internal sealed class ArtistObject
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<string> Genres { get; init; } = [];

    public IReadOnlyList<ImageObject> Images { get; init; } = [];

    [JsonPropertyName("followers")]
    public FollowersObject? Followers { get; init; }
}
