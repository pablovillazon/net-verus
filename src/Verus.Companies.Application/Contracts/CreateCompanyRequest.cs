namespace Verus.Companies.Application.Contracts;

/// <summary>Inbound contract for POST /api/companies.</summary>
public sealed record CreateCompanyRequest(string? Name, string? WebsiteUrl);
