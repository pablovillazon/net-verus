using Verus.Companies.Application.Relevance;
using Verus.Companies.Domain;

namespace Verus.Companies.Application.Contracts;

internal static class CompanyMappings
{
    public static CompanyResponse ToResponse(this Company company, double? searchScore = null) =>
        new(company.Id, company.Name, company.WebsiteUrl.AbsoluteUri, WebsiteDomain.GetHost(company.WebsiteUrl),
            company.CreatedAtUtc, searchScore);
}
