namespace Verus.Companies.Application.Relevance;

/// <summary>Domain helpers. Deliberately dependency-free (no Public Suffix List); see the heuristic below.</summary>
public static class WebsiteDomain
{
    // Common second-level labels under two-letter country TLDs (acme.co.uk, acme.com.au, acme.org.bo ...).
    private static readonly HashSet<string> SecondLevelLabels =
        new(StringComparer.Ordinal) { "co", "com", "org", "net", "gov", "edu", "ac" };

    /// <summary>Lower-case host without a leading "www.".</summary>
    public static string GetHost(Uri website)
    {
        var host = website.IdnHost.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    /// <summary>
    /// The brand-bearing label of the registrable domain: "shop.acme.com" -> "acme", "acme.co.uk" -> "acme".
    /// Heuristic: strip the TLD, and also a known second-level label when the TLD is a 2-letter country code.
    /// </summary>
    public static string GetBrandLabel(Uri website)
    {
        var labels = GetHost(website).Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length < 2)
        {
            return labels.Length == 1 ? labels[0] : string.Empty;
        }

        var suffixLabels = labels.Length >= 3 && labels[^1].Length == 2 && SecondLevelLabels.Contains(labels[^2]) ? 2 : 1;
        return labels[^(suffixLabels + 1)];
    }

    /// <summary>The brand label split on hyphens: "acme-tools" -> ["acme", "tools"].</summary>
    public static IReadOnlyList<string> GetBrandLabelParts(Uri website) =>
        GetBrandLabel(website).Split('-', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Normalizes a user-typed domain filter ("https://WWW.Acme.com/") to "acme.com".</summary>
    public static string NormalizeFilter(string filter)
    {
        var text = filter.Trim().ToLowerInvariant();
        if (text.Contains("://", StringComparison.Ordinal) && Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            text = uri.IdnHost;
        }

        text = text.TrimEnd('/');
        return text.StartsWith("www.", StringComparison.Ordinal) ? text[4..] : text;
    }
}
