namespace Verus.Companies.Application.Contracts;

/// <summary>
/// Everything a caller can ask when listing companies. Adding pagination or sorting later means adding
/// members here; neither the service signature nor the controller action signature changes shape.
/// </summary>
/// <param name="Name">Partial or full company name (case/accent-insensitive).</param>
/// <param name="Domain">Partial or full website domain, e.g. "acme" or "acme.com".</param>
/// <param name="Search">Free-text relevance search; results are ordered by relevance.</param>
public sealed record CompanyQuery(string? Name = null, string? Domain = null, string? Search = null);
