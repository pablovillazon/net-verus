using System.Globalization;
using System.Text;

namespace Verus.Companies.Application.Relevance;

/// <summary>Shared text normalization so name/domain/content/search comparisons all speak the same language.</summary>
public static class TextNormalizer
{
    // Legal forms and filler words carry no brand information ("Acme Inc" vs "acme.com").
    private static readonly HashSet<string> NoiseWords = new(StringComparer.Ordinal)
    {
        "inc", "incorporated", "llc", "llp", "ltd", "limited", "corp", "corporation", "co", "company",
        "gmbh", "sa", "ag", "bv", "plc", "the", "and", "of", "com", "www"
    };

    /// <summary>Lower-cases, strips accents and punctuation; word separators become single spaces.</summary>
    public static string Fold(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var decomposed = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = true;

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark || ch is '\'' or '’' or '`' or '&')
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Folds and splits into words, dropping legal-form noise ("Inc", "LLC"...). If that would leave nothing
    /// (a company literally called "The Company"), the unfiltered words are used instead.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string? input)
    {
        var words = Fold(input).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var meaningful = words.Where(w => !NoiseWords.Contains(w)).ToArray();
        return meaningful.Length > 0 ? meaningful : words;
    }
}
