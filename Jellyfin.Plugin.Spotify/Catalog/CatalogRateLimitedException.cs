using System;

namespace Jellyfin.Plugin.Spotify.Catalog;

/// <summary>
/// The catalog refused the request because the caller is sending too many.
/// </summary>
/// <remarks>
/// Deliberately an exception rather than a null body: null means "not found"
/// and gets cached as such. A rate-limited lookup must not be remembered as a
/// miss, or every item touched during the burst stays unmatched for a day.
/// </remarks>
public class CatalogRateLimitedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogRateLimitedException"/> class.
    /// </summary>
    public CatalogRateLimitedException()
        : base("Spotify rate limited the request.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogRateLimitedException"/> class,
    /// carrying the wait the catalog asked for.
    /// </summary>
    /// <param name="retryAfter">
    /// How long Spotify asked the caller to wait, from the <c>Retry-After</c>
    /// header. Null when it did not say.
    /// </param>
    public CatalogRateLimitedException(TimeSpan? retryAfter)
        : base("Spotify rate limited the request.")
    {
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogRateLimitedException"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    public CatalogRateLimitedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogRateLimitedException"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Inner exception.</param>
    public CatalogRateLimitedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets how long the catalog asked the caller to wait, when it said.
    /// </summary>
    public TimeSpan? RetryAfter { get; }
}
