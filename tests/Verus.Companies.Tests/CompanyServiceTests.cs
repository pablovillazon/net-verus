using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Verus.Companies.Application.Abstractions;
using Verus.Companies.Application.Contracts;
using Verus.Companies.Application.Relevance;
using Verus.Companies.Application.Services;
using Verus.Companies.Application.Validation;
using Verus.Companies.Infrastructure;

namespace Verus.Companies.Tests;

public class CompanyServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryCompanyRepository _repository = new();
    private readonly FakeMetadataProvider _metadata = new();

    private CompanyService CreateService(IWebsiteMetadataProvider? provider = null)
    {
        var evaluator = new NameWebsiteRelevanceEvaluator(
            [new DomainSimilarityScorer(), new WebsiteContentScorer(provider ?? _metadata, NullLogger<WebsiteContentScorer>.Instance)],
            Options.Create(new RelevanceOptions()));
        return new CompanyService(_repository, evaluator, new FakeTimeProvider(Now), NullLogger<CompanyService>.Instance);
    }

    private static CreateCompanyRequest Request(string? name, string? url) => new() { Name = name, WebsiteUrl = url };

    private async Task Seed(CompanyService service)
    {
        await service.CreateAsync(Request("Acme Tools Inc", "https://www.acme-tools.com"));
        await service.CreateAsync(Request("International Business Machines", "https://www.ibm.com"));
        await service.CreateAsync(Request("Acme Bakery", "https://www.acmebakery.com"));
        await service.CreateAsync(Request("Globex Corporation", "https://globex.com"));
    }

    // ---------- create ----------

    [Fact]
    public async Task Create_persists_and_returns_a_dto()
    {
        var created = await CreateService().CreateAsync(Request("  Example  ", "https://www.example.com"));

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Example", created.Name);                       // trimmed
        Assert.Equal("https://www.example.com/", created.WebsiteUrl);
        Assert.Equal("example.com", created.Domain);
        Assert.Equal(Now, created.CreatedAtUtc);                     // clock is injected
        Assert.Equal(1, (await _repository.GetAllAsync()).Count);
    }

    [Theory]
    [InlineData(null, "https://www.example.com", "Name")]
    [InlineData("", "https://www.example.com", "Name")]
    [InlineData("ab", "https://www.example.com", "Name")]
    [InlineData("Example", null, "WebsiteUrl")]
    [InlineData("Example", "example.com", "WebsiteUrl")]
    [InlineData("Example", "ftp://example.com", "WebsiteUrl")]
    public async Task Create_rejects_invalid_shape_and_stores_nothing(string? name, string? url, string expectedMember)
    {
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => CreateService().CreateAsync(Request(name, url)));

        Assert.True(ex.Errors.ContainsKey(expectedMember));
        Assert.Empty(await _repository.GetAllAsync());
    }

    [Fact]
    public async Task Create_reports_all_shape_errors_together()
    {
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => CreateService().CreateAsync(Request("", "nope")));
        Assert.Equal(2, ex.Errors.Count);
    }

    [Fact]
    public async Task Create_rejects_a_name_unrelated_to_the_website()
    {
        var ex = await Assert.ThrowsAsync<RequestValidationException>(
            () => CreateService().CreateAsync(Request("Acme Corp", "https://www.example.com")));

        Assert.True(ex.Errors.ContainsKey("Relevance"));
        Assert.Contains("example.com", ex.Message);
        Assert.Empty(await _repository.GetAllAsync());
    }

    [Fact]
    public async Task Create_accepts_an_unrelated_domain_when_the_page_content_vouches_for_the_name()
    {
        var provider = new FakeMetadataProvider(new() { ["abc.xyz"] = new("Alphabet Inc. Investor Relations", null, null) });

        var created = await CreateService(provider).CreateAsync(Request("Alphabet", "https://abc.xyz"));

        Assert.Equal("Alphabet", created.Name);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task Create_does_not_inspect_the_website_when_the_domain_already_matches()
    {
        await CreateService().CreateAsync(Request("Example", "https://www.example.com"));
        Assert.Equal(0, _metadata.Calls);
    }

    // ---------- get by id ----------

    [Fact]
    public async Task GetById_returns_the_company_or_null()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Request("Example", "https://www.example.com"));

        Assert.Equal(created.Id, (await service.GetByIdAsync(created.Id))!.Id);
        Assert.Null(await service.GetByIdAsync(Guid.NewGuid()));
    }

    // ---------- search ----------

    [Fact]
    public async Task Empty_query_returns_everything_in_creation_order()
    {
        var service = CreateService();
        await Seed(service);

        var all = await service.SearchAsync(new CompanyQuery());

        Assert.Equal(4, all.Count);
        Assert.Equal("Acme Tools Inc", all[0].Name);
        Assert.Null(all[0].SearchScore);
    }

    [Fact]
    public async Task Empty_store_returns_an_empty_list_not_an_error()
    {
        Assert.Empty(await CreateService().SearchAsync(new CompanyQuery(Search: "anything")));
    }

    [Theory]
    [InlineData("acme", 2)]
    [InlineData("ACME", 2)]
    [InlineData("machines", 1)]
    [InlineData("zzz", 0)]
    public async Task Name_filter_matches_partial_text_case_insensitively(string name, int expected)
    {
        var service = CreateService();
        await Seed(service);

        Assert.Equal(expected, (await service.SearchAsync(new CompanyQuery(Name: name))).Count);
    }

    [Theory]
    [InlineData("ibm.com", 1)]
    [InlineData("https://www.IBM.com/", 1)]
    [InlineData("acme", 2)]
    [InlineData("nothing.org", 0)]
    public async Task Domain_filter_matches_full_or_partial_domain(string domain, int expected)
    {
        var service = CreateService();
        await Seed(service);

        Assert.Equal(expected, (await service.SearchAsync(new CompanyQuery(Domain: domain))).Count);
    }

    [Fact]
    public async Task Filters_combine_with_AND()
    {
        var service = CreateService();
        await Seed(service);

        var results = await service.SearchAsync(new CompanyQuery(Name: "acme", Domain: "bakery"));

        Assert.Equal(1, results.Count);
        Assert.Equal("Acme Bakery", results[0].Name);
    }

    [Fact]
    public async Task Relevance_search_orders_best_match_first_and_exposes_the_score()
    {
        var service = CreateService();
        await Seed(service);

        var results = await service.SearchAsync(new CompanyQuery(Search: "acme bakery"));

        Assert.Equal("Acme Bakery", results[0].Name);
        Assert.Equal(1.0, results[0].SearchScore!.Value, 2);
        Assert.True(results.Count < 4);                                   // unrelated companies are excluded
        for (var i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].SearchScore >= results[i].SearchScore);
        }
    }

    [Fact]
    public async Task Relevance_search_supports_prefixes_and_typos()
    {
        var service = CreateService();
        await Seed(service);

        Assert.Equal("Globex Corporation", (await service.SearchAsync(new CompanyQuery(Search: "glob")))[0].Name);
        Assert.Equal("Globex Corporation", (await service.SearchAsync(new CompanyQuery(Search: "globx")))[0].Name);
    }

    [Fact]
    public async Task Relevance_search_can_match_on_the_domain()
    {
        var service = CreateService();
        await Seed(service);

        Assert.Equal("International Business Machines", (await service.SearchAsync(new CompanyQuery(Search: "ibm")))[0].Name);
    }
}
