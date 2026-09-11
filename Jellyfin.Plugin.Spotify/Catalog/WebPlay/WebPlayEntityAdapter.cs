using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.Spotify.Catalog.Models;

namespace Jellyfin.Plugin.Spotify.Catalog.WebPlay;

internal static class WebPlayEntityAdapter
{
    internal static Track ReadTrack(string json, string id) => Read(json, "track", id, ConvertTrack);

    internal static Album ReadAlbum(string json, string id) => Read(json, "album", id, ConvertAlbum);

    internal static Artist ReadArtist(string json, string id) => Read(json, "artist", id, ConvertArtist);

    private static T Read<T>(string json, string type, string id, Func<JsonElement, string, T> convert)
    {
        var expected = $"spotify:{type}:{id}";
        try
        {
            using var document = JsonDocument.Parse(json);
            var items = document.RootElement.GetProperty("entities").GetProperty("items");
            if (!items.TryGetProperty(expected, out var entity)
                || entity.ValueKind != JsonValueKind.Object
                || !string.Equals(String(entity, "uri"), expected, StringComparison.Ordinal))
            {
                throw new WebPlayEntityProtocolException($"The Spotify entity state does not contain the expected URI {expected}.");
            }

            return convert(entity, id);
        }
        catch (WebPlayEntityProtocolException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new WebPlayEntityProtocolException("The Spotify entity state does not follow the expected JSON schema.", ex);
        }
    }

    private static Track ConvertTrack(JsonElement value, string id)
    {
        var album = Object(value, "albumOfTrack");
        var artists = ReadArtistRefs(value, "firstArtist").Concat(ReadArtistRefs(value, "otherArtists")).ToList();
        return new Track
        {
            Id = id,
            Name = RequiredString(value, "name"),
            TrackNumber = Integer(value, "trackNumber"),
            DiscNumber = Integer(value, "discNumber"),
            DurationMs = NestedInteger(value, "duration", "totalMilliseconds"),
            Explicit = string.Equals(NestedString(value, "contentRating", "label"), "EXPLICIT", StringComparison.OrdinalIgnoreCase),
            Artists = artists,
            Album = album is null ? null : new AlbumRef
            {
                Id = WebPlaySearchAdapter.SpotifyId(String(album.Value, "uri")) ?? string.Empty,
                Name = String(album.Value, "name") ?? string.Empty,
                ReleaseDate = Date(album.Value),
                Images = Images(album.Value),
            },
        };
    }

    private static Album ConvertAlbum(JsonElement value, string id)
        => new()
        {
            Id = id,
            Name = RequiredString(value, "name"),
            AlbumType = String(value, "type")?.ToLowerInvariant(),
            ReleaseDate = Date(value),
            Copyrights = Array(Object(value, "copyright"), "items").Select(item => String(item, "text")).Where(text => text is not null).Select(text => text!).ToList(),
            Artists = ReadArtistRefs(value, "artists"),
            Images = Images(value),
            Tracks = Array(Object(value, "tracksV2"), "items").Select(UnwrapTrack).Select(track => ConvertTrackListItem(track)).Where(track => track is not null).Select(track => track!).ToList(),
        };

    private static Artist ConvertArtist(JsonElement value, string id)
        => new()
        {
            Id = id,
            Name = NestedString(value, "profile", "name") ?? throw new WebPlayEntityProtocolException("The Spotify artist entity has no name."),
            Images = Images(value),
        };

    private static Track? ConvertTrackListItem(JsonElement value)
    {
        var id = WebPlaySearchAdapter.SpotifyId(String(value, "uri"));
        return id is null ? null : new Track
        {
            Id = id,
            Name = String(value, "name") ?? string.Empty,
            TrackNumber = Integer(value, "trackNumber"),
            DurationMs = NestedInteger(value, "duration", "totalMilliseconds"),
            Artists = ReadArtistRefs(value, "artists"),
        };
    }

    private static JsonElement UnwrapTrack(JsonElement value) => value.TryGetProperty("track", out var track) ? track : value;

    private static IReadOnlyList<ArtistRef> ReadArtistRefs(JsonElement value, string property)
        => Array(Object(value, property), "items").Select(item => new ArtistRef
        {
            Id = WebPlaySearchAdapter.SpotifyId(String(item, "uri")) ?? string.Empty,
            Name = NestedString(item, "profile", "name") ?? string.Empty,
        }).Where(item => item.Id.Length > 0).ToList();

    private static IReadOnlyList<Image> Images(JsonElement value)
    {
        var visuals = Object(value, "visuals");
        var section = Object(value, "coverArt") ?? (visuals is null ? null : Object(visuals.Value, "avatarImage"));
        return Array(section, "sources").Select(source => new Image
        {
            Url = String(source, "url") ?? string.Empty,
            Width = Integer(source, "width"),
            Height = Integer(source, "height"),
        }).Where(image => image.Url.Length > 0).ToList();
    }

    private static string? Date(JsonElement value)
    {
        var date = Object(value, "date");
        var year = date is null ? 0 : Integer(date.Value, "year");
        var month = date is null ? 0 : Integer(date.Value, "month");
        var day = date is null ? 0 : Integer(date.Value, "day");
        return year <= 0 ? null : month <= 0 ? year.ToString(CultureInfo.InvariantCulture)
            : day <= 0 ? $"{year:D4}-{month:D2}" : $"{year:D4}-{month:D2}-{day:D2}";
    }

    private static JsonElement? Object(JsonElement value, string property)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var child) && child.ValueKind == JsonValueKind.Object ? child : null;

    private static IEnumerable<JsonElement> Array(JsonElement? value, string property)
        => value is { } element && element.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array ? array.EnumerateArray() : [];

    private static string RequiredString(JsonElement value, string property)
        => String(value, property) ?? throw new WebPlayEntityProtocolException($"The Spotify entity has no {property}.");

    private static string? String(JsonElement value, string property)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var child) && child.ValueKind == JsonValueKind.String ? child.GetString() : null;

    private static int Integer(JsonElement value, string property)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var child) && child.TryGetInt32(out var number) ? number : 0;

    private static string? NestedString(JsonElement value, string parent, string property)
        => Object(value, parent) is { } nested ? String(nested, property) : null;

    private static int NestedInteger(JsonElement value, string parent, string property)
        => Object(value, parent) is { } nested ? Integer(nested, property) : 0;
}
