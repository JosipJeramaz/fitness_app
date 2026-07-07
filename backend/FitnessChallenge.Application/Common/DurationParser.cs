using System.Globalization;
using System.Text.RegularExpressions;

namespace FitnessChallenge.Application.Common;

/// <summary>
/// Parses the <c>mm:ss</c> duration format. Valid only when it matches <c>^\d{1,}:[0-5]\d$</c>
/// (minutes ≥ 0, seconds 00–59) AND resolves to &gt; 0 total seconds.
/// </summary>
public static partial class DurationParser
{
    [GeneratedRegex(@"^\d{1,}:[0-5]\d$")]
    private static partial Regex Pattern();

    public static bool TryParse(string? value, out int totalSeconds)
    {
        totalSeconds = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value.Trim();
        if (!Pattern().IsMatch(text))
            return false;

        var separator = text.IndexOf(':');
        if (!int.TryParse(text.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
            return false; // guards against absurdly large minute values overflowing int
        if (!int.TryParse(text.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            return false;

        var total = (long)minutes * 60 + seconds;
        if (total is <= 0 or > int.MaxValue)
            return false;

        totalSeconds = (int)total;
        return true;
    }
}
