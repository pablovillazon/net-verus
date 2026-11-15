using Verus.Companies.Application.Relevance;
using Verus.Companies.Application.Validation;

namespace Verus.Companies.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("  Café   Müller ", "cafe muller")]
    [InlineData("AT&T", "att")]
    [InlineData("John's Bakery", "johns bakery")]
    [InlineData("Acme-Tools, Inc.", "acme tools inc")]
    public void Fold_normalizes_case_accents_and_punctuation(string input, string expected) =>
        Assert.Equal(expected, TextNormalizer.Fold(input));

    [Fact]
    public void Tokenize_drops_legal_form_noise() =>
        Assert.Equal(new[] { "acme", "tools" }, TextNormalizer.Tokenize("The Acme Tools Inc."));

    [Fact]
    public void Tokenize_keeps_words_when_everything_is_noise() =>
        Assert.Equal(new[] { "the", "company" }, TextNormalizer.Tokenize("The Company"));
}

public class WebsiteDomainTests
{
    [Theory]
    [InlineData("https://www.example.com", "example.com", "example")]
    [InlineData("https://shop.acme-tools.com/path", "shop.acme-tools.com", "acme-tools")]
    [InlineData("https://www.acme.co.uk", "acme.co.uk", "acme")]
    [InlineData("https://acme.com.bo", "acme.com.bo", "acme")]
    public void Host_and_brand_label_are_extracted(string url, string host, string label)
    {
        var uri = new Uri(url);
        Assert.Equal(host, WebsiteDomain.GetHost(uri));
        Assert.Equal(label, WebsiteDomain.GetBrandLabel(uri));
    }

    [Theory]
    [InlineData("https://WWW.Acme.com/", "acme.com")]
    [InlineData("Acme.com", "acme.com")]
    [InlineData("  www.acme.com/ ", "acme.com")]
    public void Domain_filter_is_normalized(string filter, string expected) =>
        Assert.Equal(expected, WebsiteDomain.NormalizeFilter(filter));
}

public class WebsiteUrlParserTests
{
    [Theory]
    [InlineData("https://www.example.com")]
    [InlineData("http://example.org/path?x=1")]
    [InlineData("  https://sub.example.co.uk  ")]
    public void Valid_urls_are_accepted(string input)
    {
        Assert.True(WebsiteUrlParser.TryParse(input, out var uri, out var error));
        Assert.NotNull(uri);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("example.com")]
    [InlineData("www.example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("https://localhost")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://user:pw@example.com")]
    [InlineData("https://exa mple.com")]
    [InlineData("https://example.")]
    [InlineData("javascript:alert(1)")]
    public void Invalid_urls_are_rejected_with_a_message(string input)
    {
        Assert.False(WebsiteUrlParser.TryParse(input, out var uri, out var error));
        Assert.Null(uri);
        Assert.NotNull(error);
    }

    [Fact]
    public void Null_is_reported_as_required()
    {
        Assert.False(WebsiteUrlParser.TryParse(null, out _, out var error));
        Assert.Equal("Website URL is required.", error);
    }
}
