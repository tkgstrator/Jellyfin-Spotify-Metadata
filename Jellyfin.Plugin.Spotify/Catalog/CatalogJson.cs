using System.Text.Json;

namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// Serializer settings shared by every catalog response.
/// </summary>
public static class CatalogJson
{
    /// <summary>
    /// Gets the options used to deserialize Spotify responses, whose
    /// property names are camelCase.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
