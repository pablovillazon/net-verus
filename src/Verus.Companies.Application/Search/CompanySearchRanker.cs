using Verus.Companies.Application.Relevance;
using Verus.Companies.Domain;

namespace Verus.Companies.Application.Search;

/// <summary>
/// Scores how well a stored company answers a free-text query (0..1). Unrelated to the submission-time
/// name-vs-website check: this ranks results, that one guards writes.
/// For every query word the best of these applies: exact word (1.0), word prefix (0.85, supports
/// type-ahead), substring (0.6), typo-tolerant match (similarity x 0.6). The field score is the mean over
/// query words. A company's score is the better of its name and (slightly discounted) domain label.
/// </summary>
internal static class CompanySearchRanker
{
    public const double MinimumScore = 0.3;

    private const double DomainWeight = 0.8;
    private const double FuzzyThreshold = 0.75;

    public static double Score(string query, Company company)
    {
        var queryWords = TextNormalizer.Tokenize(query);
        if (queryWords.Count == 0)
        {
            return 0;
        }

        var compactQuery = string.Concat(queryWords);
        var nameScore = FieldScore(queryWords, compactQuery, TextNormalizer.Tokenize(company.Name));
        var domainScore = FieldScore(queryWords, compactQuery, WebsiteDomain.GetBrandLabelParts(company.WebsiteUrl));

        return Math.Round(Math.Max(nameScore, DomainWeight * domainScore), 3);
    }

    private static double FieldScore(IReadOnlyList<string> queryWords, string compactQuery, IReadOnlyList<string> fieldWords)
    {
        if (fieldWords.Count == 0)
        {
            return 0;
        }

        var compactField = string.Concat(fieldWords);
        if (compactQuery == compactField)
        {
            return 1.0;
        }

        return queryWords.Average(word => WordScore(word, fieldWords, compactField));
    }

    private static double WordScore(string word, IReadOnlyList<string> fieldWords, string compactField)
    {
        if (fieldWords.Contains(word))
        {
            return 1.0;
        }

        if (word.Length >= 2 && fieldWords.Any(f => f.StartsWith(word, StringComparison.Ordinal)))
        {
            return 0.85;
        }

        if (word.Length >= 3 && compactField.Contains(word, StringComparison.Ordinal))
        {
            return 0.6;
        }

        if (word.Length >= 4)
        {
            var best = fieldWords.Max(f => StringSimilarity.LevenshteinRatio(word, f));
            if (best >= FuzzyThreshold)
            {
                return best * 0.6;
            }
        }

        return 0;
    }
}
