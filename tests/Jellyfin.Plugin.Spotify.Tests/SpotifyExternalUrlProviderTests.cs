using System.Linq;
using Jellyfin.Plugin.Spotify.ExternalIds;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.Spotify.Tests;

public class SpotifyExternalUrlProviderTests
{
    [Fact]
    public void GetExternalUrls_BuildsAnAlbumLink()
    {
        var album = new MusicAlbum();
        album.SetProviderId(ProviderKeys.Album, "4aawyAB9vmqN3uQ7FjRGTy");

        var url = Assert.Single(new SpotifyExternalUrlProvider().GetExternalUrls(album));

        Assert.Equal("https://open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy", url);
    }

    [Fact]
    public void GetExternalUrls_BuildsAnArtistLink()
    {
        var artist = new MusicArtist();
        artist.SetProviderId(ProviderKeys.Artist, "0OdUWJ0sBjDrqHygGUXeCF");

        var url = Assert.Single(new SpotifyExternalUrlProvider().GetExternalUrls(artist));

        Assert.Equal("https://open.spotify.com/artist/0OdUWJ0sBjDrqHygGUXeCF", url);
    }

    [Fact]
    public void GetExternalUrls_BuildsATrackLink()
    {
        var song = new Audio();
        song.SetProviderId(ProviderKeys.Song, "11dFghVXANMlKmJXsNCbNl");

        var url = Assert.Single(new SpotifyExternalUrlProvider().GetExternalUrls(song));

        Assert.Equal("https://open.spotify.com/track/11dFghVXANMlKmJXsNCbNl", url);
    }

    [Fact]
    public void GetExternalUrls_YieldsNothingWithoutAnId()
        => Assert.Empty(new SpotifyExternalUrlProvider().GetExternalUrls(new MusicAlbum()));
}
