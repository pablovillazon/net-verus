using Verus.Companies.Domain;

namespace Verus.Companies.Application.Abstractions;

/// <summary>
/// Persistence port. Defined in the Application layer and implemented in Infrastructure
/// (Dependency Inversion). Async even though the current store is in memory, so that
/// introducing a database later does not change this contract or any caller.
/// </summary>
public interface ICompanyRepository
{
    Task AddAsync(Company company, CancellationToken cancellationToken = default);

    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default);
}
