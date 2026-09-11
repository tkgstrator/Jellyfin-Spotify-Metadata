using System;

namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// The catalog rejected the plugin's credentials or its access token.
/// </summary>
/// <remarks>
/// Separate from <see cref="CatalogRateLimitedException"/> because the cure is
/// different: a refusal here is not waited out, it is fixed by the user on the
/// settings page. Like that one, it is an exception rather than a null body so
/// the cache never records it as "not found".
/// </remarks>
public class CatalogAuthenticationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogAuthenticationException"/> class.
    /// </summary>
    public CatalogAuthenticationException()
        : base("Spotify rejected the plugin's credentials.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogAuthenticationException"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    public CatalogAuthenticationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogAuthenticationException"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Inner exception.</param>
    public CatalogAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
