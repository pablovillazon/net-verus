using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Verus.Companies.Application.Abstractions;
using Verus.Companies.Application.Contracts;

namespace Verus.Companies.Tests;

/// <summary>
/// End-to-end HTTP behaviour through the real pipeline (routing, model binding, validation, exception handler).
/// Each test gets a fresh host, hence a fresh in-memory store. Network access is replaced by a fake provider.
/// </summary>
public sealed class CompaniesApiTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CompaniesApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IWebsiteMetadataProvider>();
                services.AddSingleton<IWebsiteMetadataProvider>(new FakeMetadataProvider());
            }));
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> Post(string? name, string? url) =>
        _client.PostAsJsonAsync("/api/companies", new { name, websiteUrl = url });

    [Fact]
    public async Task Post_valid_company_returns_201_with_location_and_dto()
    {
        var response = await Post("Example", "https://www.example.com");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
        Assert.NotNull(body);
        Assert.Equal("Example", body.Name);
        Assert.Equal($"/api/companies/{body.Id}", response.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Post_invalid_input_returns_400_with_field_errors()
    {
        var response = await Post("ab", "not-a-url");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("WebsiteUrl", out _));
    }

    [Fact]
    public async Task Post_irrelevant_name_returns_400_with_a_concise_message()
    {
        var response = await Post("Acme Corp", "https://www.example.com");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("example.com", json.RootElement.GetProperty("detail").GetString()!);
    }

    [Fact]
    public async Task Post_malformed_json_returns_400() =>
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.PostAsync("/api/companies", new StringContent("{bad", System.Text.Encoding.UTF8, "application/json"))).StatusCode);

    [Fact]
    public async Task Get_all_on_an_empty_store_returns_200_and_an_empty_array()
    {
        var response = await _client.GetAsync("/api/companies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<CompanyResponse>>())!);
    }

    [Fact]
    public async Task Get_by_id_returns_200_for_existing_and_404_for_unknown()
    {
        var created = (await (await Post("Example", "https://www.example.com")).Content.ReadFromJsonAsync<CompanyResponse>())!;

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/companies/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/companies/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Get_with_filters_and_relevance_query_works_over_http()
    {
        await Post("Acme Tools", "https://www.acme-tools.com");
        await Post("International Business Machines", "https://www.ibm.com");

        var byName = await _client.GetFromJsonAsync<List<CompanyResponse>>("/api/companies?name=acme");
        var byDomain = await _client.GetFromJsonAsync<List<CompanyResponse>>("/api/companies?domain=ibm.com");
        var ranked = await _client.GetFromJsonAsync<List<CompanyResponse>>("/api/companies?q=machines");
        var none = await _client.GetFromJsonAsync<List<CompanyResponse>>("/api/companies?name=zzz");

        Assert.Equal(1, byName!.Count);
        Assert.Equal(1, byDomain!.Count);
        Assert.Equal("International Business Machines", ranked![0].Name);
        Assert.NotNull(ranked[0].SearchScore);
        Assert.Empty(none!);
    }

    [Fact]
    public async Task Health_endpoint_is_up() =>
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);
}
