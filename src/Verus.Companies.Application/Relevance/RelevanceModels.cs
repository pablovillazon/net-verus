using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Verus.Companies.Application.Relevance;

/// <summary>Configuration for the name-to-website relevance check (section "Relevance").</summary>
public sealed class RelevanceOptions
{
    public const string SectionName = "Relevance";

    /// <summary>Minimum score (0..1) the best signal must reach for a name to be considered relevant.</summary>
    [Range(0.0, 1.0)]
    public double MinimumScore { get; set; } = 0.5;
}

/// <summary>One piece of evidence from one scorer, with a human-readable reason (explainability).</summary>
public sealed record RelevanceSignal(string Source, double Score, string Explanation);

public sealed record RelevanceAssessment(
    bool IsRelevant,
    double Score,
    double Threshold,
    IReadOnlyList<RelevanceSignal> Signals)
{
    public string DescribeFailure(string companyName, string host) =>
        string.Create(CultureInfo.InvariantCulture,
            $"Company name '{companyName}' does not appear to be related to '{host}' (relevance {Score:0.00}, required {Threshold:0.00}).");

    public string DescribeSignals() =>
        string.Join("; ", Signals.Select(s => string.Create(CultureInfo.InvariantCulture, $"{s.Source}={s.Score:0.00} ({s.Explanation})")));
}

/// <summary>A strategy that produces one relevance signal for a (company name, website) pair.</summary>
public interface IRelevanceScorer
{
    /// <summary>Returns null when the scorer has no opinion (e.g. the site could not be inspected).</summary>
    Task<RelevanceSignal?> ScoreAsync(string companyName, Uri website, CancellationToken cancellationToken = default);
}

public interface INameWebsiteRelevanceEvaluator
{
    Task<RelevanceAssessment> EvaluateAsync(string companyName, Uri website, CancellationToken cancellationToken = default);
}
