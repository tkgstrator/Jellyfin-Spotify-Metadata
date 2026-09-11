using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Spotify.Catalog.Models;

namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// Selects a Spotify image for a requested artwork size.
/// </summary>
public static class ArtworkSelector
{
    /// <summary>
    /// Selects the image whose edge is closest to <paramref name="artworkSize"/>.
    /// </summary>
    /// <param name="images">Candidate images.</param>
    /// <param name="artworkSize">Requested edge length in pixels.</param>
    /// <returns>The closest image, preferring the larger image on a tie; otherwise null.</returns>
    public static Image? Select(IReadOnlyList<Image>? images, int artworkSize)
        => images?
            .Where(image => !string.IsNullOrWhiteSpace(image.Url))
            .OrderBy(image => Math.Abs((long)Math.Max(image.Width, image.Height) - artworkSize))
            .ThenByDescending(image => Math.Max(image.Width, image.Height))
            .FirstOrDefault();
}
