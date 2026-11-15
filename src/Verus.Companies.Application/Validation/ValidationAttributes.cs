using System.ComponentModel.DataAnnotations;
using Verus.Companies.Domain;

namespace Verus.Companies.Application.Validation;

/// <summary>Company name: required, and between the configured min/max length after trimming.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class CompanyNameAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var members = new[] { validationContext.MemberName ?? "Name" };
        var name = (value as string)?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return new ValidationResult("Company name is required.", members);
        }

        if (name.Length < CompanyRules.NameMinLength)
        {
            return new ValidationResult(
                $"Company name must be at least {CompanyRules.NameMinLength} characters long.", members);
        }

        if (name.Length > CompanyRules.NameMaxLength)
        {
            return new ValidationResult(
                $"Company name must not exceed {CompanyRules.NameMaxLength} characters.", members);
        }

        return ValidationResult.Success;
    }
}

/// <summary>Website URL: required and a well-formed absolute http(s) URL (see <see cref="WebsiteUrlParser"/>).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class HttpUrlAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (WebsiteUrlParser.TryParse(value as string, out _, out var error))
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(error, [validationContext.MemberName ?? "WebsiteUrl"]);
    }
}
