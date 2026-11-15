using System.Globalization;

namespace Verus.Companies.Application.Relevance;

/// <summary>
/// Offline, deterministic, explainable scorer: compares the company name with the brand label of the domain.
/// The score is the best of three independent evidences:
///  1. word coverage  - what share of the name's words appear in (or fuzzily match) the domain label;
///  2. acronym        - the label equals the initials of the name (International Business Machines -> ibm);
///  3. overall string - Levenshtein similarity of the compacted name and label (tolerates typos, hyphens).
/// </summary>
public sealed class DomainSimilarityScorer : IRelevanceScorer
{
    public const string SourceName = "domain";

    private const double FuzzyWordThreshold = 0.8;

    public Task<RelevanceSignal?> ScoreAsync(string companyName, Uri website, CancellationToken cancellationToken = default)
        => Task.FromResult<RelevanceSignal?>(Score(companyName, website));

    internal static RelevanceSignal Score(string companyName, Uri website)
    {
        var words = TextNormalizer.Tokenize(companyName);
        var labelParts = WebsiteDomain.GetBrandLabelParts(website);
        var label = string.Concat(labelParts);

        if (words.Count == 0 || label.Length == 0)
        {
            return new RelevanceSignal(SourceName, 0, "The name or the domain has no comparable words.");
        }

        var compactName = string.Concat(words);

        var matchedWords = words.Count(w => WordMatchesLabel(w, label, labelParts));
        var coverage = (double)matchedWords / words.Count;
        var acronym = words.Count >= 2 && label == string.Concat(words.Select(w => w[0])) ? 1.0 : 0.0;
        var similarity = StringSimilarity.LevenshteinRatio(compactName, label);

        double score;
        string explanation;
        if (acronym >= 1.0)
        {
            score = acronym;
            explanation = $"Domain label '{label}' is an acronym of the company name.";
        }
        else if (coverage >= similarity)
        {
            score = coverage;
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"Domain label '{label}' matches {matchedWords} of {words.Count} words of the company name.");
        }
        else
        {
            score = similarity;
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"Domain label '{label}' is {similarity:P0} similar to the company name.");
        }

        return new RelevanceSignal(SourceName, Math.Round(score, 3), explanation);
    }

    private static bool WordMatchesLabel(string word, string compactLabel, IReadOnlyList<string> labelParts)
    {
        if (word.Length >= 3 && compactLabel.Contains(word, StringComparison.Ordinal))
        {
            return true;
        }

        if (labelParts.Contains(word))
        {
            return true; // short words must match a whole label part exactly (avoids "a" matching everything)
        }

        return word.Length >= 4 && labelParts.Any(part => StringSimilarity.LevenshteinRatio(word, part) >= FuzzyWordThreshold);
    }
}
