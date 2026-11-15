namespace Verus.Companies.Application.Abstractions;

/// <summary>Publicly visible descriptive metadata of a web page.</summary>
public sealed record WebsiteMetadata(string? Title, string? SiteName, string? Description);

/// <summary>
/// Port for "content inspection". Returns null when the site cannot be inspected (unreachable, not HTML,
/// blocked, inspection disabled...). A null result is never an error: relevance simply falls back to the
/// remaining signals.
/// </summary>
public interface IWebsiteMetadataProvider
{
    Task<WebsiteMetadata?> GetAsync(Uri website, CancellationToken cancellationToken = default);
}
