using System.Globalization;
using Microsoft.Extensions.Logging;
using Verus.Companies.Application.Abstractions;

namespace Verus.Companies.Application.Relevance;

/// <summary>
/// Content-inspection scorer: checks whether the words of the company name appear in what the website says
/// about itself. The page title and og:site_name are strong evidence; the description is weaker (it is long,
/// so words match more easily) and is therefore discounted.
/// </summary>
public sealed class WebsiteContentScorer(IWebsiteMetadataProvider metadataProvider, ILogger<WebsiteContentScorer> logger)
    : IRelevanceScorer
{
    public const string SourceName = "content";

    private const double DescriptionWeight = 0.8;

    public async Task<RelevanceSignal?> ScoreAsync(string companyName, Uri website, CancellationToken cancellationToken = default)
    {
        var words = TextNormalizer.Tokenize(companyName);
        if (words.Count == 0)
        {
            return null;
        }

        var metadata = await metadataProvider.GetAsync(website, cancellationToken);
        if (metadata is null)
        {
            logger.LogDebug("No website metadata available for {Host}; content signal skipped.", WebsiteDomain.GetHost(website));
            return null;
        }

        var headline = TextNormalizer.Fold($"{metadata.Title} {metadata.SiteName}");
        var description = TextNormalizer.Fold(metadata.Description);

        var (headlineScore, headlineMatches) = Coverage(words, headline);
        var (descriptionScore, descriptionMatches) = Coverage(words, description);
        descriptionScore *= DescriptionWeight;

        return headlineScore >= descriptionScore
            ? new RelevanceSignal(SourceName, Math.Round(headlineScore, 3),
                string.Create(CultureInfo.InvariantCulture, $"Page title/site name mentions {headlineMatches} of {words.Count} words of the company name."))
            : new RelevanceSignal(SourceName, Math.Round(descriptionScore, 3),
                string.Create(CultureInfo.InvariantCulture, $"Page description mentions {descriptionMatches} of {words.Count} words of the company name."));
    }

    private static (double Score, int Matches) Coverage(IReadOnlyList<string> words, string foldedText)
    {
        if (foldedText.Length == 0)
        {
            return (0, 0);
        }

        var textWords = foldedText.Split(' ').ToHashSet(StringComparer.Ordinal);
        var matches = words.Count(w => textWords.Contains(w) || (w.Length >= 4 && foldedText.Contains(w, StringComparison.Ordinal)));
        return ((double)matches / words.Count, matches);
    }
}
