namespace Verus.Companies.Application.Contracts;

/// <summary>
/// Outbound contract. The only shape of a company that ever leaves the application layer;
/// the domain entity and the storage structures stay internal.
/// </summary>
public sealed record CompanyResponse(Guid Id, string Name, string WebsiteUrl, DateTimeOffset CreatedAtUtc);
