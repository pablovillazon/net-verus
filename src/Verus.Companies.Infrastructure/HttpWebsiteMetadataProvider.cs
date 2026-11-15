using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Verus.Companies.Application.Abstractions;
using Verus.Companies.Application.Relevance;

namespace Verus.Companies.Infrastructure;

/// <summary>
/// Fetches a page (bounded by timeout and size) and extracts its metadata. Any failure degrades to "no
/// opinion" (null) so a slow or hostile website can never break or block a submission for long.
/// </summary>
internal sealed class HttpWebsiteMetadataProvider(
    HttpClient httpClient,
    IOptions<WebsiteInspectionOptions> options,
    ILogger<HttpWebsiteMetadataProvider> logger) : IWebsiteMetadataProvider
{
    public async Task<WebsiteMetadata?> GetAsync(Uri website, CancellationToken cancellationToken = default)
    {
        var host = WebsiteDomain.GetHost(website);
        try
        {
            using var response = await httpClient.GetAsync(website, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation("Content inspection of {Host} skipped: HTTP {StatusCode}", host, (int)response.StatusCode);
                return null;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not ("text/html" or "application/xhtml+xml"))
            {
                logger.LogInformation("Content inspection of {Host} skipped: content type {ContentType}", host, mediaType);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var html = await ReadLimitedAsync(stream, options.Value.MaxResponseBytes, cancellationToken);
            return HtmlMetadataParser.Parse(html);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Content inspection of {Host} timed out after {Seconds}s", host, options.Value.TimeoutSeconds);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or RegexMatchTimeoutException)
        {
            logger.LogWarning("Content inspection of {Host} failed: {Reason}", host, ex.Message);
            return null;
        }
    }

    private static async Task<string> ReadLimitedAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while (buffer.Length < maxBytes && (read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
