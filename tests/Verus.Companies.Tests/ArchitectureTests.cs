using System.Reflection;
using Verus.Companies.Application.Services;
using Verus.Companies.Domain;

namespace Verus.Companies.Tests;

/// <summary>Executable guard-rails for the layering rules described in docs/ARCHITECTURE.md.</summary>
public class ArchitectureTests
{
    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!);

    [Fact]
    public void Domain_depends_on_nothing_but_the_base_library() =>
        Assert.Empty(References(typeof(Company).Assembly)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal) && n != "netstandard" && n != "mscorlib"));

    [Fact]
    public void Application_does_not_depend_on_ASP_NET_Core_http_or_mvc_or_on_infrastructure()
    {
        var references = References(typeof(CompanyService).Assembly).ToList();

        Assert.Empty(references.Where(n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)));
        Assert.DoesNotContain("Verus.Companies.Infrastructure", references);
        Assert.DoesNotContain("Verus.Companies.Api", references);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_api() =>
        Assert.DoesNotContain("Verus.Companies.Api", References(typeof(Verus.Companies.Infrastructure.DependencyInjection).Assembly));

    [Fact]
    public void The_in_memory_store_is_not_part_of_the_public_surface() =>
        Assert.DoesNotContain(typeof(Verus.Companies.Infrastructure.DependencyInjection).Assembly.GetExportedTypes(),
            t => t.Name.Contains("InMemory", StringComparison.Ordinal));

    [Fact]
    public void The_service_contract_never_exposes_domain_types()
    {
        // ICompanyRepository legitimately speaks in domain entities (it is the port to storage);
        // what callers of the application layer see (ICompanyService + Contracts DTOs) must not.
        var domainNamespace = typeof(Company).Namespace!;

        var serviceSignatures = typeof(ICompanyService).GetMethods()
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType));
        var dtoProperties = typeof(CompanyService).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == "Verus.Companies.Application.Contracts")
            .SelectMany(t => t.GetProperties())
            .Select(p => p.PropertyType);

        Assert.Empty(serviceSignatures.Concat(dtoProperties)
            .Where(type => type.ToString().Contains(domainNamespace, StringComparison.Ordinal))
            .Select(type => type.ToString()));
    }
}
