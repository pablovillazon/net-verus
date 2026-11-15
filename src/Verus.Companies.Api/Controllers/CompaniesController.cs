using Microsoft.AspNetCore.Mvc;
using Verus.Companies.Application.Contracts;
using Verus.Companies.Application.Services;

namespace Verus.Companies.Api.Controllers;

/// <summary>
/// Thin HTTP adapter: binds the request, calls the service, picks the status code. No business logic,
/// no try/catch (failures are translated centrally by <see cref="Infrastructure.GlobalExceptionHandler"/>).
/// </summary>
[ApiController]
[Route("api/companies")]
[Produces("application/json")]
public sealed class CompaniesController(ICompanyService companies) : ControllerBase
{
    /// <summary>Submits a company. The name must be relevant to the website.</summary>
    [HttpPost]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompanyResponse>> Create(CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var created = await companies.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Lists companies. With no parameters returns everything (creation order). <c>name</c> and <c>domain</c> are
    /// partial-match filters; <c>q</c> is a relevance search that orders results best-first. An empty result is 200 with [].
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CompanyResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyResponse>>> Search(
        [FromQuery] string? name,
        [FromQuery] string? domain,
        [FromQuery(Name = "q")] string? search,
        CancellationToken cancellationToken)
    {
        var results = await companies.SearchAsync(new CompanyQuery(name, domain, search), cancellationToken);
        return Ok(results);
    }

    /// <summary>Gets one company by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(id, cancellationToken);
        return company is null
            ? Problem(detail: $"Company '{id}' was not found.", statusCode: StatusCodes.Status404NotFound, title: "Company not found")
            : Ok(company);
    }
}
