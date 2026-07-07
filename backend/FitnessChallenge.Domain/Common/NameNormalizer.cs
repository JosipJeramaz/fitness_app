using System.Text.RegularExpressions;

namespace FitnessChallenge.Domain.Common;

/// <summary>
/// Single source of truth for name normalization used by the user uniqueness rule.
/// Trims, collapses internal whitespace runs to a single space, and upper-cases using the
/// invariant culture so that e.g. <c>"John"</c> and <c>"  john "</c> collide.
/// </summary>
public static partial class NameNormalizer
{
    /// <summary>Normalizes a single name component (first or last name).</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return WhitespaceRegex().Replace(value.Trim(), " ").ToUpperInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
