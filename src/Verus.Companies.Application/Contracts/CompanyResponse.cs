namespace Verus.Companies.Application.Contracts;

/// <summary>
/// Outbound contract; the only shape of a company that ever leaves the application layer.
/// <paramref name="SearchScore"/> is only populated for relevance searches (<c>?q=</c>).
/// </summary>
public sealed record CompanyResponse(
    Guid Id,
    string Name,
    string WebsiteUrl,
    string Domain,
    DateTimeOffset CreatedAtUtc,
    double? SearchScore = null);
