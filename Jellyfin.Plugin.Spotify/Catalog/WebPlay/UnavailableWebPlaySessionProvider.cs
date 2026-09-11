using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Spotify.Catalog.WebPlay;

/// <summary>
/// Reports that automatic Web Player token bootstrap is intentionally unavailable.
/// </summary>
public sealed class UnavailableWebPlaySessionProvider : IWebPlaySessionProvider
{
    private const string Message =
        "WebPlay is selected, but no anonymous Web Player session provider is configured. " +
        "Automatic bootstrap is unavailable because Spotify's unpublished TOTP material must not be stored by the plugin.";

    /// <inheritdoc />
    public Task<WebPlaySession> GetSessionAsync(CancellationToken cancellationToken)
        => Task.FromException<WebPlaySession>(new InvalidOperationException(Message));

    /// <inheritdoc />
    public void Invalidate()
    {
    }
}
