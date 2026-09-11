using System;

namespace Jellyfin.Plugin.Spotify.Catalog.WebPlay;

/// <summary>
/// Indicates that a public Spotify entity page no longer follows the expected protocol.
/// </summary>
public sealed class WebPlayEntityProtocolException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebPlayEntityProtocolException"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    public WebPlayEntityProtocolException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebPlayEntityProtocolException"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Underlying error.</param>
    public WebPlayEntityProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
