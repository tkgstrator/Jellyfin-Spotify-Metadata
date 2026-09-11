using System;

namespace Jellyfin.Plugin.Spotify.Catalog.WebPlay;

/// <summary>
/// Credentials attached to a Web Player Pathfinder request.
/// </summary>
/// <param name="AccessToken">Anonymous Web Player access token.</param>
/// <param name="ClientVersion">Dynamic Spotify Web Player client version.</param>
public sealed record WebPlaySession(string AccessToken, string ClientVersion)
{
    /// <summary>
    /// Validates that both required session values are present.
    /// </summary>
    /// <exception cref="InvalidOperationException">A required value is absent.</exception>
    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(AccessToken) || string.IsNullOrWhiteSpace(ClientVersion))
        {
            throw new InvalidOperationException(
                "WebPlay requires an anonymous access token and the current Web Player client version. " +
                "Automatic token bootstrap is unavailable because it depends on unpublished obfuscated material.");
        }
    }
}
