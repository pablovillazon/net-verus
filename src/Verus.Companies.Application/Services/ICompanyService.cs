using Verus.Companies.Application.Contracts;

namespace Verus.Companies.Application.Services;

public interface ICompanyService
{
    /// <exception cref="Validation.RequestValidationException">The request violates a validation or relevance rule.</exception>
    Task<CompanyResponse> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default);

    /// <returns>The company, or null when it does not exist.</returns>
    Task<CompanyResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists companies; an empty result is a normal outcome, never an error.</summary>
    Task<IReadOnlyList<CompanyResponse>> SearchAsync(CompanyQuery query, CancellationToken cancellationToken = default);
}
