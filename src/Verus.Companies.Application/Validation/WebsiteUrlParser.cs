using System.Diagnostics.CodeAnalysis;

namespace Verus.Companies.Application.Validation;

/// <summary>The one definition of "a valid website URL", shared by the attribute and the service.</summary>
public static class WebsiteUrlParser
{
    public const string FormatMessage =
        "Website URL must be a valid absolute http(s) URL with a domain name, for example https://www.example.com.";

    public static bool TryParse(string? input, [NotNullWhen(true)] out Uri? uri, [NotNullWhen(false)] out string? error)
    {
        uri = null;
        var text = input?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            error = "Website URL is required.";
            return false;
        }

        var hasHttpScheme = text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                            || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (!hasHttpScheme
            || text.Any(char.IsWhiteSpace)
            || !Uri.TryCreate(text, UriKind.Absolute, out var parsed)
            || parsed.HostNameType != UriHostNameType.Dns   // rejects IP literals and empty hosts
            || !string.IsNullOrEmpty(parsed.UserInfo)       // rejects https://user:pass@host
            || !parsed.IdnHost.Contains('.')                // rejects "localhost" and single-label hosts
            || parsed.IdnHost.EndsWith('.'))
        {
            error = FormatMessage;
            return false;
        }

        uri = parsed;
        error = null;
        return true;
    }
}
