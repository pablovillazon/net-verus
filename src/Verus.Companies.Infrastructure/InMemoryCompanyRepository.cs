using System.Collections.Concurrent;
using Verus.Companies.Application.Abstractions;
using Verus.Companies.Domain;

namespace Verus.Companies.Infrastructure;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ICompanyRepository"/>.
/// Registered as a singleton. Companies are immutable, so returning references
/// does not leak mutable internal state; collections are returned as snapshots.
/// </summary>
internal sealed class InMemoryCompanyRepository : ICompanyRepository
{
    private readonly ConcurrentDictionary<Guid, Company> _store = new();

    public Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        if (!_store.TryAdd(company.Id, company))
        {
            throw new InvalidOperationException($"A company with id '{company.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.TryGetValue(id, out var company) ? company : null);

    public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Company> snapshot = _store.Values
            .OrderBy(c => c.CreatedAtUtc)
            .ToList();

        return Task.FromResult(snapshot);
    }
}
