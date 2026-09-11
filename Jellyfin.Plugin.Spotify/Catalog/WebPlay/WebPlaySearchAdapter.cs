using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.Spotify.Catalog.Models;

namespace Jellyfin.Plugin.Spotify.Catalog.WebPlay;

internal static class WebPlaySearchAdapter
{
    internal static WebPlayPage<Track> ReadTracks(string json) => ReadPage(json, "tracksV2", ReadTrack);

    internal static WebPlayPage<Album> ReadAlbums(string json) => ReadPage(json, "albumsV2", ReadAlbum);

    internal static WebPlayPage<Artist> ReadArtists(string json) => ReadPage(json, "artists", ReadArtist);

    internal static string? SpotifyId(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        var separator = uri.LastIndexOf(':');
        return separator >= 0 && separator + 1 < uri.Length ? uri[(separator + 1)..] : null;
    }

    private static WebPlayPage<T> ReadPage<T>(string json, string field, Func<JsonElement, T?> convert)
        where T : class
    {
        using var document = JsonDocument.Parse(json);
        if (!FindProperty(document.RootElement, field, out var section))
        {
            return new WebPlayPage<T>([], null);
        }

        var results = new List<T>();
        if (FindProperty(section, "items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var wrapper in items.EnumerateArray())
            {
                var item = Unwrap(wrapper);
                var converted = convert(item);
                if (converted is not null)
                {
                    results.Add(converted);
                }
            }
        }

        int? nextOffset = null;
        if (FindProperty(section, "pagingInfo", out var paging)
            && paging.TryGetProperty("nextOffset", out var next)
            && next.ValueKind == JsonValueKind.Number
            && next.TryGetInt32(out var value))
        {
            nextOffset = value;
        }

        return new WebPlayPage<T>(results, nextOffset);
    }

    private static Track? ReadTrack(JsonElement value)
    {
        var id = SpotifyId(String(value, "uri"));
        if (id is null)
        {
            return null;
        }

        var album = Object(value, "albumOfTrack");
        return new Track
        {
            Id = id,
            Name = String(value, "name") ?? string.Empty,
            TrackNumber = Integer(value, "trackNumber"),
            DiscNumber = Integer(value, "discNumber"),
            DurationMs = NestedInteger(value, "duration", "totalMilliseconds"),
            Explicit = string.Equals(NestedString(value, "contentRating", "label"), "EXPLICIT", StringComparison.OrdinalIgnoreCase),
            Artists = ReadArtistsRefs(value),
            Album = album is null ? null : ReadAlbumRef(album.Value),
        };
    }

    private static Album? ReadAlbum(JsonElement value)
    {
        var id = SpotifyId(String(value, "uri"));
        if (id is null)
        {
            return null;
        }

        return new Album
        {
            Id = id,
            Name = String(value, "name") ?? string.Empty,
            AlbumType = String(value, "type")?.ToLowerInvariant(),
            ReleaseDate = ReadDate(value),
            Artists = ReadArtistsRefs(value),
            Images = ReadImages(value),
        };
    }

    private static Artist? ReadArtist(JsonElement value)
    {
        var id = SpotifyId(String(value, "uri"));
        if (id is null)
        {
            return null;
        }

        return new Artist
        {
            Id = id,
            Name = NestedString(value, "profile", "name") ?? String(value, "name") ?? string.Empty,
            Genres = ReadNames(value, "genres"),
            Images = ReadImages(value),
        };
    }

    private static AlbumRef? ReadAlbumRef(JsonElement value)
    {
        var id = SpotifyId(String(value, "uri"));
        return id is null ? null : new AlbumRef
        {
            Id = id,
            Name = String(value, "name") ?? string.Empty,
            ReleaseDate = ReadDate(value),
            Images = ReadImages(value),
        };
    }

    private static IReadOnlyList<ArtistRef> ReadArtistsRefs(JsonElement value)
    {
        if (!FindProperty(value, "artists", out var artists)
            || !FindProperty(artists, "items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray().Select(Unwrap).Select(artist => new ArtistRef
        {
            Id = SpotifyId(String(artist, "uri")) ?? string.Empty,
            Name = NestedString(artist, "profile", "name") ?? String(artist, "name") ?? string.Empty,
        }).Where(artist => artist.Id.Length > 0).ToList();
    }

    private static IReadOnlyList<Image> ReadImages(JsonElement value)
    {
        if (!FindProperty(value, "sources", out var sources) || sources.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return sources.EnumerateArray().Select(source => new Image
        {
            Url = String(source, "url") ?? string.Empty,
            Width = Integer(source, "width"),
            Height = Integer(source, "height"),
        }).Where(image => image.Url.Length > 0).ToList();
    }

    private static IReadOnlyList<string> ReadNames(JsonElement value, string property)
    {
        if (!FindProperty(value, property, out var section)
            || !FindProperty(section, "items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray().Select(item => String(Unwrap(item), "name"))
            .Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToList();
    }

    private static string? ReadDate(JsonElement value)
    {
        var date = Object(value, "date");
        if (date is null || Integer(date.Value, "year") <= 0)
        {
            return null;
        }

        var year = Integer(date.Value, "year");
        var month = Integer(date.Value, "month");
        var day = Integer(date.Value, "day");
        return month <= 0 ? year.ToString(CultureInfo.InvariantCulture)
            : day <= 0 ? string.Format(CultureInfo.InvariantCulture, "{0:D4}-{1:D2}", year, month)
            : string.Format(CultureInfo.InvariantCulture, "{0:D4}-{1:D2}-{2:D2}", year, month, day);
    }

    private static JsonElement Unwrap(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("item", out var item))
        {
            value = item;
        }

        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty("data", out var data) ? data : value;
    }

    private static JsonElement? Object(JsonElement value, string property)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.Object
            ? found
            : null;

    private static string? String(JsonElement value, string property)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var found) && found.ValueKind == JsonValueKind.String
            ? found.GetString()
            : null;

    private static int Integer(JsonElement value, string property)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var found) && found.TryGetInt32(out var number) ? number : 0;

    private static string? NestedString(JsonElement value, string parent, string property)
        => Object(value, parent) is { } nested ? String(nested, property) : null;

    private static int NestedInteger(JsonElement value, string parent, string property)
        => Object(value, parent) is { } nested ? Integer(nested, property) : 0;

    private static bool FindProperty(JsonElement value, string property, out JsonElement found)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty(property, out found))
            {
                return true;
            }

            foreach (var child in value.EnumerateObject())
            {
                if (FindProperty(child.Value, property, out found))
                {
                    return true;
                }
            }
        }

        found = default;
        return false;
    }
}
