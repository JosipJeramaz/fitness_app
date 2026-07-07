using FitnessChallenge.Domain.Enums;

namespace FitnessChallenge.Api.Common;

/// <summary>Parses the optional <c>sport</c> filter query value (accepts wire names plus "dailySteps").</summary>
public static class SportQueryParser
{
    private static readonly IReadOnlyDictionary<string, ActivityType> Map =
        new Dictionary<string, ActivityType>(StringComparer.OrdinalIgnoreCase)
        {
            ["running"] = ActivityType.Running,
            ["walking"] = ActivityType.Walking,
            ["cycling"] = ActivityType.Cycling,
            ["gym"] = ActivityType.Gym,
            ["swimming"] = ActivityType.Swimming,
            ["dailysteps"] = ActivityType.DailySteps,
            ["daily-steps"] = ActivityType.DailySteps,
            ["daily_steps"] = ActivityType.DailySteps
        };

    public static ActivityType? Parse(string? value)
        => value is not null && Map.TryGetValue(value.Trim(), out var type) ? type : null;
}
