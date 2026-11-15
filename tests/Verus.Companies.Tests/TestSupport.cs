using Verus.Companies.Application.Abstractions;

namespace Verus.Companies.Tests;

internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Content-inspection stand-in: returns canned metadata per host, or null (site unreachable).</summary>
internal sealed class FakeMetadataProvider(Dictionary<string, WebsiteMetadata>? byHost = null) : IWebsiteMetadataProvider
{
    public int Calls { get; private set; }

    public Task<WebsiteMetadata?> GetAsync(Uri website, CancellationToken cancellationToken = default)
    {
        Calls++;
        WebsiteMetadata? result = null;
        byHost?.TryGetValue(website.IdnHost.ToLowerInvariant(), out result);
        return Task.FromResult(result);
    }
}
