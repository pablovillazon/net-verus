using Verus.Companies.Domain;

namespace Verus.Companies.Application.Contracts;

internal static class CompanyMappings
{
    public static CompanyResponse ToResponse(this Company company) =>
        new(company.Id, company.Name, company.WebsiteUrl.AbsoluteUri, company.CreatedAtUtc);
}
