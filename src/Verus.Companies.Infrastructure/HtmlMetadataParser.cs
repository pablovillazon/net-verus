using System.Net;
using System.Text.RegularExpressions;
using Verus.Companies.Application.Abstractions;

namespace Verus.Companies.Infrastructure;

/// <summary>
/// Extracts title, og:site_name and description from raw HTML. A full HTML parser would be heavier than this
/// need warrants; regexes run with a match timeout so hostile markup cannot cause catastrophic backtracking.
/// </summary>
internal static partial class HtmlMetadataParser
{
    public static WebsiteMetadata Parse(string html)
    {
        var title = Clean(TitleRegex().Match(html) is { Success: true } t ? t.Groups[1].Value : null);

        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match tag in MetaTagRegex().Matches(html))
        {
            string? key = null;
            string? content = null;
            foreach (Match attribute in AttributeRegex().Matches(tag.Value))
            {
                var name = attribute.Groups[1].Value.ToLowerInvariant();
                var value = attribute.Groups[2].Success ? attribute.Groups[2].Value : attribute.Groups[3].Value;
                if (name is "property" or "name")
                {
                    key ??= value.ToLowerInvariant();
                }
                else if (name == "content")
                {
                    content = value;
                }
            }

            if (key is not null && content is not null)
            {
                meta.TryAdd(key, content);
            }
        }

        var siteName = Clean(meta.GetValueOrDefault("og:site_name"));
        title ??= Clean(meta.GetValueOrDefault("og:title"));
        var description = Clean(meta.GetValueOrDefault("description") ?? meta.GetValueOrDefault("og:description"));

        return new WebsiteMetadata(title, siteName, description);
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return WhitespaceRegex().Replace(WebUtility.HtmlDecode(value), " ").Trim();
    }

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 250)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<meta\s[^>]*>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 250)]
    private static partial Regex MetaTagRegex();

    [GeneratedRegex("""([a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(?:"([^"]*)"|'([^']*)')""", RegexOptions.None, matchTimeoutMilliseconds: 250)]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 250)]
    private static partial Regex WhitespaceRegex();
}
