using Verus.Companies.Application.Abstractions;

namespace Verus.Companies.Infrastructure;

/// <summary>Used when content inspection is disabled: never touches the network.</summary>
internal sealed class NullWebsiteMetadataProvider : IWebsiteMetadataProvider
{
    public Task<WebsiteMetadata?> GetAsync(Uri website, CancellationToken cancellationToken = default) =>
        Task.FromResult<WebsiteMetadata?>(null);
}
