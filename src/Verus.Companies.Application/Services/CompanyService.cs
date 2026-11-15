using Microsoft.Extensions.Logging;
using Verus.Companies.Application.Abstractions;
using Verus.Companies.Application.Contracts;
using Verus.Companies.Application.Relevance;
using Verus.Companies.Application.Search;
using Verus.Companies.Application.Validation;
using Verus.Companies.Domain;

namespace Verus.Companies.Application.Services;

public sealed class CompanyService(
    ICompanyRepository repository,
    INameWebsiteRelevanceEvaluator relevanceEvaluator,
    TimeProvider timeProvider,
    ILogger<CompanyService> logger) : ICompanyService
{
    public async Task<CompanyResponse> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Shape rules (required, length, URL format).
        RequestValidator.ValidateOrThrow(request);
        if (!WebsiteUrlParser.TryParse(request.WebsiteUrl, out var website, out var urlError))
        {
            throw new RequestValidationException(nameof(CreateCompanyRequest.WebsiteUrl), urlError);
        }

        // 2. Semantic rule: the name must be relevant to the website.
        var name = request.Name!.Trim();
        var host = WebsiteDomain.GetHost(website);
        var assessment = await relevanceEvaluator.EvaluateAsync(name, website, cancellationToken);
        if (!assessment.IsRelevant)
        {
            logger.LogWarning(
                "Company submission rejected: name '{CompanyName}' is not relevant to {Domain}. Score {Score:0.00}, required {Threshold:0.00}. Signals: {Signals}",
                name, host, assessment.Score, assessment.Threshold, assessment.DescribeSignals());
            throw new RequestValidationException("Relevance", assessment.DescribeFailure(name, host));
        }

        // 3. Persist.
        var company = new Company(Guid.NewGuid(), name, website, timeProvider.GetUtcNow());
        await repository.AddAsync(company, cancellationToken);

        logger.LogInformation("Company {CompanyId} created for domain {Domain} (relevance {Score:0.00})",
            company.Id, host, assessment.Score);
        return company.ToResponse();
    }

    public async Task<CompanyResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await repository.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            logger.LogInformation("Company {CompanyId} was not found", id);
        }

        return company?.ToResponse();
    }

    public async Task<IReadOnlyList<CompanyResponse>> SearchAsync(CompanyQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IEnumerable<Company> candidates = await repository.GetAllAsync(cancellationToken);

        if (Clean(query.Name) is { } name)
        {
            var folded = TextNormalizer.Fold(name);
            candidates = candidates.Where(c => TextNormalizer.Fold(c.Name).Contains(folded, StringComparison.Ordinal));
        }

        if (Clean(query.Domain) is { } domain)
        {
            var wanted = WebsiteDomain.NormalizeFilter(domain);
            candidates = candidates.Where(c => WebsiteDomain.GetHost(c.WebsiteUrl).Contains(wanted, StringComparison.Ordinal));
        }

        IReadOnlyList<CompanyResponse> results;
        if (Clean(query.Search) is { } search)
        {
            results = candidates
                .Select(c => (Company: c, Score: CompanySearchRanker.Score(search, c)))
                .Where(x => x.Score >= CompanySearchRanker.MinimumScore)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Company.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Company.ToResponse(x.Score))
                .ToList();
        }
        else
        {
            results = candidates.Select(c => c.ToResponse()).ToList(); // repository order = creation order
        }

        logger.LogInformation("Company search (name: {Name}, domain: {Domain}, q: {Search}) returned {Count} result(s)",
            query.Name, query.Domain, query.Search, results.Count);
        return results;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
