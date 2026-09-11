namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// The application credentials the Spotify Web API is called with.
/// </summary>
/// <param name="ClientId">Client id of a Spotify application.</param>
/// <param name="ClientSecret">Client secret of that application.</param>
/// <remarks>
/// These belong to the person running the server, taken from their own
/// Spotify developer dashboard. They are never committed and never shipped
/// with the plugin.
/// </remarks>
public sealed record SpotifyCredentials(string ClientId, string ClientSecret);
