using System.ComponentModel.DataAnnotations;

namespace Verus.Companies.Infrastructure;

/// <summary>Settings for the optional outbound content inspection (section "WebsiteInspection").</summary>
public sealed class WebsiteInspectionOptions
{
    public const string SectionName = "WebsiteInspection";

    /// <summary>When false, no outbound request is ever made and relevance relies on the domain alone.</summary>
    public bool Enabled { get; set; } = true;

    [Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 3;

    [Range(1_024, 5_000_000)]
    public int MaxResponseBytes { get; set; } = 256 * 1024;
}
