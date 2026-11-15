using Microsoft.Extensions.Options;
using Verus.Companies.Application.Abstractions;
using Verus.Companies.Application.Relevance;

namespace Verus.Companies.Tests;

public class DomainSimilarityScorerTests
{
    private static double Score(string name, string url) =>
        DomainSimilarityScorer.Score(name, new Uri(url)).Score;

    [Theory]
    [InlineData("Example", "https://www.example.com")]
    [InlineData("Acme Tools Inc", "https://www.acme-tools.com")]
    [InlineData("Acme Tools", "https://acme.com")]                       // brand word matches
    [InlineData("International Business Machines", "https://www.ibm.com")] // acronym
    [InlineData("Gogle", "https://www.google.com")]                      // typo tolerance
    [InlineData("Café Müller GmbH", "https://www.cafe-muller.de")]       // accents + legal form
    [InlineData("AT&T", "https://www.att.com")]
    public void Related_names_reach_the_default_threshold(string name, string url) =>
        Assert.True(Score(name, url) >= 0.5, $"{name} vs {url} scored {Score(name, url)}");

    [Theory]
    [InlineData("Acme Corp", "https://www.example.com")]
    [InlineData("Microsoft", "https://www.apple.com")]
    [InlineData("Pizza Palace", "https://www.google.com")]
    public void Unrelated_names_stay_below_the_default_threshold(string name, string url) =>
        Assert.True(Score(name, url) < 0.5, $"{name} vs {url} scored {Score(name, url)}");

    [Fact]
    public void Signal_explains_itself()
    {
        var signal = DomainSimilarityScorer.Score("Acme Tools", new Uri("https://acme.com"));
        Assert.Equal("domain", signal.Source);
        Assert.Contains("acme", signal.Explanation);
    }
}

public class WebsiteContentScorerTests
{
    private static WebsiteContentScorer Scorer(WebsiteMetadata? metadata) =>
        new(new FakeMetadataProvider(metadata is null ? null : new() { ["abc.xyz"] = metadata }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WebsiteContentScorer>.Instance);

    [Fact]
    public async Task Title_mentioning_the_name_scores_high()
    {
        var signal = await Scorer(new("Alphabet - Investor Relations", null, null))
            .ScoreAsync("Alphabet", new Uri("https://abc.xyz"));

        Assert.NotNull(signal);
        Assert.Equal(1.0, signal.Score, 2);
    }

    [Fact]
    public async Task Description_only_evidence_is_discounted()
    {
        var signal = await Scorer(new("Home", null, "We are Alphabet, a holding company."))
            .ScoreAsync("Alphabet", new Uri("https://abc.xyz"));

        Assert.NotNull(signal);
        Assert.Equal(0.8, signal.Score, 2);
    }

    [Fact]
    public async Task Unreachable_site_yields_no_opinion()
    {
        var signal = await Scorer(null).ScoreAsync("Alphabet", new Uri("https://abc.xyz"));
        Assert.Null(signal);
    }
}

public class NameWebsiteRelevanceEvaluatorTests
{
    private sealed class StubScorer(double? score, string source) : IRelevanceScorer
    {
        public int Calls { get; private set; }

        public Task<RelevanceSignal?> ScoreAsync(string companyName, Uri website, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(score is null ? null : new RelevanceSignal(source, score.Value, "stub"));
        }
    }

    private static NameWebsiteRelevanceEvaluator Evaluator(params IRelevanceScorer[] scorers) =>
        new(scorers, Options.Create(new RelevanceOptions { MinimumScore = 0.5 }));

    [Fact]
    public async Task Stops_at_first_scorer_that_reaches_the_threshold()
    {
        var cheap = new StubScorer(0.9, "cheap");
        var expensive = new StubScorer(1.0, "expensive");

        var result = await Evaluator(cheap, expensive).EvaluateAsync("X", new Uri("https://x.com"));

        Assert.True(result.IsRelevant);
        Assert.Equal(1, cheap.Calls);
        Assert.Equal(0, expensive.Calls);
    }

    [Fact]
    public async Task Falls_through_to_later_scorers_and_takes_the_best_signal()
    {
        var result = await Evaluator(new StubScorer(0.1, "domain"), new StubScorer(0.8, "content"))
            .EvaluateAsync("X", new Uri("https://x.com"));

        Assert.True(result.IsRelevant);
        Assert.Equal(0.8, result.Score, 2);
        Assert.Equal(2, result.Signals.Count);
    }

    [Fact]
    public async Task Scorers_without_an_opinion_are_ignored()
    {
        var result = await Evaluator(new StubScorer(0.2, "domain"), new StubScorer(null, "content"))
            .EvaluateAsync("X", new Uri("https://x.com"));

        Assert.False(result.IsRelevant);
        Assert.Equal(1, result.Signals.Count);
    }

    [Fact]
    public async Task No_signals_means_not_relevant()
    {
        var result = await Evaluator().EvaluateAsync("X", new Uri("https://x.com"));
        Assert.False(result.IsRelevant);
        Assert.Equal(0.0, result.Score, 2);
    }
}
