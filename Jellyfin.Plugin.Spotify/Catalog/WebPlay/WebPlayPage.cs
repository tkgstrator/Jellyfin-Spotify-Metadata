using System.Collections.Generic;

namespace Jellyfin.Plugin.Spotify.Catalog.WebPlay;

internal sealed record WebPlayPage<T>(IReadOnlyList<T> Items, int? NextOffset);
