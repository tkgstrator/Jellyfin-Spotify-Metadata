using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Spotify.Catalog.WebPlay;

/// <summary>
/// Reads entity data embedded in anonymous public Spotify pages.
/// </summary>
public sealed class WebPlayEntityTransport : IWebPlayEntityTransport
{
    /// <summary>
    /// Maximum accepted HTML response size.
    /// </summary>
    public const int MaximumHtmlBytes = 2 * 1024 * 1024;

    private const string Prefix = "<script id=\"initialState\" type=\"text/plain\">";
    private const string Suffix = "</script>";
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebPlayEntityTransport"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client.</param>
    public WebPlayEntityTransport(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string entityType, string id, CancellationToken cancellationToken)
    {
        if (entityType is not ("track" or "album" or "artist"))
        {
            throw new ArgumentOutOfRangeException(nameof(entityType));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://open.spotify.com/{entityType}/{Uri.EscapeDataString(id)}");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumHtmlBytes)
        {
            throw new WebPlayEntityProtocolException("The Spotify entity page exceeds the permitted size.");
        }

        var bytes = await ReadLimitedAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var html = Encoding.UTF8.GetString(bytes);
        var start = html.IndexOf(Prefix, StringComparison.Ordinal);
        if (start < 0 || html.IndexOf(Prefix, start + Prefix.Length, StringComparison.Ordinal) >= 0)
        {
            throw new WebPlayEntityProtocolException("The Spotify entity page does not contain exactly one expected initialState script.");
        }

        start += Prefix.Length;
        var end = html.IndexOf(Suffix, start, StringComparison.Ordinal);
        if (end < 0 || html.IndexOf('<', start, end - start) >= 0)
        {
            throw new WebPlayEntityProtocolException("The Spotify initialState script is malformed.");
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(html[start..end]));
        }
        catch (FormatException ex)
        {
            throw new WebPlayEntityProtocolException("The Spotify initialState script is not valid Base64.", ex);
        }
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > MaximumHtmlBytes)
            {
                throw new WebPlayEntityProtocolException("The Spotify entity page exceeds the permitted size.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }
}
