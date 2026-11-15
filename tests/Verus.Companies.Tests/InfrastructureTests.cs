using System.Net;
using Verus.Companies.Infrastructure;

namespace Verus.Companies.Tests;

public class HtmlMetadataParserTests
{
    [Fact]
    public void Extracts_title_site_name_and_description()
    {
        const string html = """
            <html><head>
              <title>  Acme &amp; Sons
                 - Home </title>
              <meta property="og:site_name" content="Acme">
              <meta name='description' content='Tools &amp; more'>
            </head><body>ignored</body></html>
            """;

        var metadata = HtmlMetadataParser.Parse(html);

        Assert.Equal("Acme & Sons - Home", metadata.Title);
        Assert.Equal("Acme", metadata.SiteName);
        Assert.Equal("Tools & more", metadata.Description);
    }

    [Fact]
    public void Falls_back_to_open_graph_fields()
    {
        var metadata = HtmlMetadataParser.Parse("""<meta property="og:title" content="OG Title"><meta property="og:description" content="OG Desc">""");

        Assert.Equal("OG Title", metadata.Title);
        Assert.Equal("OG Desc", metadata.Description);
    }

    [Fact]
    public void Returns_empty_metadata_for_garbage()
    {
        var metadata = HtmlMetadataParser.Parse("<<<not html>>>");

        Assert.Null(metadata.Title);
        Assert.Null(metadata.SiteName);
        Assert.Null(metadata.Description);
    }
}

public class SsrfGuardTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("93.184.216.34")]
    [InlineData("2606:4700:4700::1111")]
    public void Public_addresses_are_allowed(string ip) => Assert.True(SsrfGuard.IsPublic(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]   // cloud metadata endpoint
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("::ffff:10.0.0.1")]   // IPv4-mapped private address
    public void Private_loopback_and_link_local_addresses_are_blocked(string ip) =>
        Assert.False(SsrfGuard.IsPublic(IPAddress.Parse(ip)));

    [Fact]
    public void Addresses_just_outside_private_ranges_are_allowed()
    {
        Assert.True(SsrfGuard.IsPublic(IPAddress.Parse("172.32.0.1")));
        Assert.True(SsrfGuard.IsPublic(IPAddress.Parse("172.15.255.255")));
    }
}
