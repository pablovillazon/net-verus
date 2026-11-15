using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Verus.Companies.Application.Abstractions;

namespace Verus.Companies.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// The seams to the outside world live here. Replacing the in-memory store with a database means swapping
    /// the ICompanyRepository registration (e.g. for an EF Core repository); nothing else changes.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(WebsiteInspectionOptions.SectionName);
        services.AddOptions<WebsiteInspectionOptions>().Bind(section).ValidateDataAnnotations().ValidateOnStart();
        var inspection = section.Get<WebsiteInspectionOptions>() ?? new WebsiteInspectionOptions();

        services.AddSingleton<ICompanyRepository, InMemoryCompanyRepository>();

        if (inspection.Enabled)
        {
            services.AddHttpClient<IWebsiteMetadataProvider, HttpWebsiteMetadataProvider>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(inspection.TimeoutSeconds);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("VerusCompanyValidator/1.0");
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    ConnectCallback = SsrfGuard.ConnectAsync,
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 3,
                });
        }
        else
        {
            services.AddSingleton<IWebsiteMetadataProvider, NullWebsiteMetadataProvider>();
        }

        return services;
    }
}
