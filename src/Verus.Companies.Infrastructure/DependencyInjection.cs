using Microsoft.Extensions.DependencyInjection;
using Verus.Companies.Application.Abstractions;

namespace Verus.Companies.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// The single seam for persistence. Replacing the in-memory store with a database
    /// means swapping this one registration (e.g. for an EF Core repository).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICompanyRepository, InMemoryCompanyRepository>();
        return services;
    }
}
