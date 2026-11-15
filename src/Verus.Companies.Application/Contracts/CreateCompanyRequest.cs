using Verus.Companies.Application.Validation;

namespace Verus.Companies.Application.Contracts;

/// <summary>
/// Inbound contract for POST /api/companies. Declared with init properties (not a positional record) so the
/// same DataAnnotations work both for ASP.NET Core model validation, which requires constructor-parameter
/// attributes on positional records, and for <c>Validator.TryValidateObject</c> inside the service, which
/// reads properties. Properties are nullable on purpose: a missing value yields a clear validation message
/// instead of a deserialization failure.
/// </summary>
public sealed record CreateCompanyRequest
{
    [CompanyName]
    public string? Name { get; init; }

    [HttpUrl]
    public string? WebsiteUrl { get; init; }
}
