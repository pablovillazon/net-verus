using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Verus.Companies.Application.Relevance;
using Verus.Companies.Application.Services;

namespace Verus.Companies.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RelevanceOptions>()
            .Bind(configuration.GetSection(RelevanceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        // Order matters: cheap offline scorer first, network-bound content scorer last (see evaluator).
        // Scoped (not singleton) because the content scorer depends on a typed HttpClient.
        services.AddScoped<IRelevanceScorer, DomainSimilarityScorer>();
        services.AddScoped<IRelevanceScorer, WebsiteContentScorer>();
        services.AddScoped<INameWebsiteRelevanceEvaluator, NameWebsiteRelevanceEvaluator>();
        services.AddScoped<ICompanyService, CompanyService>();

        return services;
    }
}
