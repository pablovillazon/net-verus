using Verus.Companies.Domain;
using Verus.Companies.Infrastructure;

namespace Verus.Companies.Tests;

public class InMemoryCompanyRepositoryTests
{
    private static Company NewCompany(string name = "Acme", DateTimeOffset? createdAt = null) =>
        new(Guid.NewGuid(), name, new Uri("https://www.acme.com"), createdAt ?? DateTimeOffset.UtcNow);

    [Fact]
    public async Task AddAsync_then_GetByIdAsync_returns_the_company()
    {
        var repository = new InMemoryCompanyRepository();
        var company = NewCompany();

        await repository.AddAsync(company);
        var found = await repository.GetByIdAsync(company.Id);

        Assert.NotNull(found);
        Assert.Equal(company.Id, found.Id);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_company_does_not_exist()
    {
        var repository = new InMemoryCompanyRepository();

        var found = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public async Task GetAllAsync_returns_empty_list_when_store_is_empty()
    {
        var repository = new InMemoryCompanyRepository();

        var all = await repository.GetAllAsync();

        Assert.Empty(all);
    }

    [Fact]
    public async Task GetAllAsync_returns_companies_in_creation_order()
    {
        var repository = new InMemoryCompanyRepository();
        var now = DateTimeOffset.UtcNow;
        var second = NewCompany("Second", now.AddMinutes(1));
        var first = NewCompany("First", now);

        await repository.AddAsync(second);
        await repository.AddAsync(first);
        var all = await repository.GetAllAsync();

        Assert.Equal(new[] { "First", "Second" }, all.Select(c => c.Name));
    }

    [Fact]
    public async Task AddAsync_throws_when_id_already_exists()
    {
        var repository = new InMemoryCompanyRepository();
        var company = NewCompany();
        await repository.AddAsync(company);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AddAsync(company));
    }

    [Fact]
    public async Task Concurrent_adds_are_all_stored()
    {
        var repository = new InMemoryCompanyRepository();

        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => repository.AddAsync(NewCompany($"Company {i}"))));
        var all = await repository.GetAllAsync();

        Assert.Equal(200, all.Count);
    }
}
