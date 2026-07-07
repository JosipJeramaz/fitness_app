using System.Globalization;

namespace FitnessChallenge.Application.Common;

/// <summary>
/// Strict-ish ISO-8601 timestamp parsing. Values without an explicit offset are assumed UTC, and the
/// result is always normalized to UTC so downstream time-window math (leaderboard trend, dashboard
/// buckets) is offset-agnostic.
/// </summary>
public static class IsoDateTime
{
    public static bool TryParse(string? value, out DateTimeOffset utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            return false;

        utc = parsed;
        return true;
    }
}
