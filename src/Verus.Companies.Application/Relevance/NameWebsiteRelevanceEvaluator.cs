using Microsoft.Extensions.Options;

namespace Verus.Companies.Application.Relevance;

/// <summary>
/// Composes the scorers. Cheap scorers run first and evaluation stops as soon as one reaches the threshold,
/// so the (slow, network-bound) content check only runs when the domain alone is not convincing.
/// The final score is the best signal: "relevant if ANY evidence supports the name". The alternative (an average)
/// would reject legitimate companies whose domain and page content disagree, e.g. Alphabet at abc.xyz.
/// </summary>
public sealed class NameWebsiteRelevanceEvaluator(IEnumerable<IRelevanceScorer> scorers, IOptions<RelevanceOptions> options)
    : INameWebsiteRelevanceEvaluator
{
    public async Task<RelevanceAssessment> EvaluateAsync(string companyName, Uri website, CancellationToken cancellationToken = default)
    {
        var threshold = options.Value.MinimumScore;
        var signals = new List<RelevanceSignal>();

        foreach (var scorer in scorers)
        {
            var signal = await scorer.ScoreAsync(companyName, website, cancellationToken);
            if (signal is null)
            {
                continue;
            }

            signals.Add(signal);
            if (signal.Score >= threshold)
            {
                break;
            }
        }

        var best = signals.Count == 0 ? 0 : signals.Max(s => s.Score);
        return new RelevanceAssessment(best >= threshold, best, threshold, signals);
    }
}
