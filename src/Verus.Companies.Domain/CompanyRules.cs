namespace Verus.Companies.Domain;

/// <summary>Single source of truth for the business rules that describe a valid company.</summary>
public static class CompanyRules
{
    public const int NameMinLength = 3;
    public const int NameMaxLength = 100;
}
