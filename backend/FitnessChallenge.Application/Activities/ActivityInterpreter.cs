using FitnessChallenge.Application.Common;
using FitnessChallenge.Application.Dtos.Activities;
using FitnessChallenge.Domain.Enums;

namespace FitnessChallenge.Application.Activities;

/// <summary>A per-field validation error (field name keys the ValidationProblemDetails response).</summary>
public sealed record ActivityFieldError(string Field, string Message);

/// <summary>
/// Result of interpreting a raw request against the validation matrix. When <see cref="IsValid"/>, the
/// resolved <see cref="Type"/> and exactly-one metric are populated; otherwise <see cref="Errors"/> explains why.
/// </summary>
public sealed class ActivityInterpretation
{
    private ActivityInterpretation(
        bool isValid,
        IReadOnlyList<ActivityFieldError> errors,
        ActivityType type,
        decimal? distanceKm,
        int? durationSeconds,
        int? steps,
        DateTimeOffset dateTimeUtc)
    {
        IsValid = isValid;
        Errors = errors;
        Type = type;
        DistanceKm = distanceKm;
        DurationSeconds = durationSeconds;
        Steps = steps;
        DateTimeUtc = dateTimeUtc;
    }

    public bool IsValid { get; }
    public IReadOnlyList<ActivityFieldError> Errors { get; }
    public ActivityType Type { get; }
    public decimal? DistanceKm { get; }
    public int? DurationSeconds { get; }
    public int? Steps { get; }
    public DateTimeOffset DateTimeUtc { get; }

    internal static ActivityInterpretation Invalid(IReadOnlyList<ActivityFieldError> errors)
        => new(false, errors, default, null, null, null, default);

    internal static ActivityInterpretation Valid(ActivityType type, decimal? distanceKm, int? durationSeconds, int? steps, DateTimeOffset dateTimeUtc)
        => new(true, Array.Empty<ActivityFieldError>(), type, distanceKm, durationSeconds, steps, dateTimeUtc);
}

/// <summary>
/// Single source of truth for the ingestion validation matrix. Both the FluentValidation validator (for the
/// 400 response) and the ingestion service (for the resolved values) go through here, so the rules can
/// never drift apart. Pure and dependency-free → trivially unit-testable.
/// </summary>
public static class ActivityInterpreter
{
    private const string SportField = "sport";
    private const string DistanceField = "distance";
    private const string DurationField = "duration";
    private const string StepsField = "steps";
    private const string DatetimeField = "datetime";

    // Per-entry plausibility caps: hard 400 on values no single activity can realistically reach.
    // Deliberately per-entry (not per-day aggregate) so ingestion stays a pure validation with no DB read.
    private const int MaxSteps = 100_000;
    private const int MaxDurationSeconds = 18 * 3600; // 18 h = 1080:00 in mm:ss

    // An activity must be worth at least 1 point after scoring, so sub-point micro-entries
    // (1 m of running, 99 steps, 0:59 of gym — all floor to 0) are rejected. Each threshold is the
    // smallest value that scores >= 1 point; the distance minimums live per-sport in the dictionary below.
    private const int MinDurationSeconds = 60; // 1 full minute -> 5 pts (gym) / 15 pts (swimming)
    private const int MinSteps = 100;          // 100 / 100 = 1 point

    // Absorbs client/server clock skew so a legitimate "log it now" is never rejected as future.
    private const int FutureToleranceMinutes = 5;

    // The min/max bounds live next to the type so a new sport cannot be added without declaring its limits.
    private static readonly IReadOnlyDictionary<string, (ActivityType Type, decimal MinKm, decimal MaxKm)> DistanceSports =
        new Dictionary<string, (ActivityType, decimal, decimal)>(StringComparer.OrdinalIgnoreCase)
        {
            ["running"] = (ActivityType.Running, 0.01m, 100m), // 0.01 km * 100 = 1 point
            ["walking"] = (ActivityType.Walking, 0.02m, 80m),  // 0.02 km * 50  = 1 point
            ["cycling"] = (ActivityType.Cycling, 0.04m, 300m), // 0.04 km * 25  = 1 point
        };

    private static readonly IReadOnlyDictionary<string, (ActivityType Type, int MaxSeconds)> DurationSports =
        new Dictionary<string, (ActivityType, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["gym"] = (ActivityType.Gym, MaxDurationSeconds),
            ["swimming"] = (ActivityType.Swimming, MaxDurationSeconds),
        };

    public static ActivityInterpretation Interpret(IngestActivityRequest request, DateTimeOffset utcNow)
    {
        var errors = new List<ActivityFieldError>();

        if (!IsoDateTime.TryParse(request.Datetime, out var dateTimeUtc))
            errors.Add(new(DatetimeField, "'datetime' is required and must be a valid ISO-8601 timestamp."));
        else if (dateTimeUtc < utcNow.AddYears(-1))
            errors.Add(new(DatetimeField, "'datetime' must not be more than one year in the past."));
        else if (dateTimeUtc > utcNow.AddMinutes(FutureToleranceMinutes))
            errors.Add(new(DatetimeField, "'datetime' must not be in the future."));

        // Presence: a metric "counts" only when meaningfully provided.
        var hasDistance = request.Distance.HasValue;
        var hasDuration = !string.IsNullOrWhiteSpace(request.Duration);
        var hasSteps = request.Steps.HasValue;
        var sport = string.IsNullOrWhiteSpace(request.Sport) ? null : request.Sport!.Trim();

        ActivityType? type = null;
        decimal? distanceKm = null;
        int? durationSeconds = null;
        int? steps = null;

        if (sport is null)
        {
            // No sport → must be Daily Steps (steps present, distance/duration absent).
            type = ActivityType.DailySteps;
            if (hasDistance) errors.Add(new(DistanceField, "Daily steps activities must not provide 'distance'."));
            if (hasDuration) errors.Add(new(DurationField, "Daily steps activities must not provide 'duration'."));
            if (!hasSteps)
                errors.Add(new(StepsField, "Daily steps activities must provide 'steps' (omit 'sport')."));
            else if (request.Steps!.Value < MinSteps)
                errors.Add(new(StepsField, $"'steps' must be at least {MinSteps} (worth at least 1 point)."));
            else if (request.Steps.Value > MaxSteps)
                errors.Add(new(StepsField, $"'steps' must be at most {MaxSteps}."));
            else
                steps = request.Steps.Value;
        }
        else if (DistanceSports.TryGetValue(sport, out var distanceEntry))
        {
            type = distanceEntry.Type;
            if (hasDuration) errors.Add(new(DurationField, $"{sport} activities must provide 'distance', not 'duration'."));
            if (hasSteps) errors.Add(new(StepsField, $"{sport} activities must provide 'distance', not 'steps'."));
            if (!hasDistance)
                errors.Add(new(DistanceField, $"{sport} activities must provide 'distance'."));
            else if (request.Distance!.Value < distanceEntry.MinKm)
                errors.Add(new(DistanceField, $"'distance' must be at least {distanceEntry.MinKm} km for {sport} (worth at least 1 point)."));
            else if (request.Distance.Value > distanceEntry.MaxKm)
                errors.Add(new(DistanceField, $"'distance' must be at most {distanceEntry.MaxKm} km for {sport}."));
            else
                distanceKm = request.Distance.Value;
        }
        else if (DurationSports.TryGetValue(sport, out var durationEntry))
        {
            type = durationEntry.Type;
            if (hasDistance) errors.Add(new(DistanceField, $"{sport} activities must provide 'duration', not 'distance'."));
            if (hasSteps) errors.Add(new(StepsField, $"{sport} activities must provide 'duration', not 'steps'."));
            if (!hasDuration)
                errors.Add(new(DurationField, $"{sport} activities must provide 'duration'."));
            else if (!DurationParser.TryParse(request.Duration, out var seconds))
                errors.Add(new(DurationField, "'duration' must match 'mm:ss' (seconds 00–59) and be greater than zero."));
            else if (seconds < MinDurationSeconds)
                errors.Add(new(DurationField, "'duration' must be at least 1 minute (1:00), worth at least 1 point."));
            else if (seconds > durationEntry.MaxSeconds)
                errors.Add(new(DurationField,
                    $"'duration' must be at most {durationEntry.MaxSeconds / 3600} hours ({durationEntry.MaxSeconds / 60}:00) for {sport}."));
            else
                durationSeconds = seconds;
        }
        else
        {
            errors.Add(new(SportField,
                $"Unknown sport '{request.Sport}'. Allowed: running, walking, cycling, gym, swimming — or omit 'sport' for daily steps."));
        }

        if (errors.Count > 0 || type is null)
            return ActivityInterpretation.Invalid(errors);

        return ActivityInterpretation.Valid(type.Value, distanceKm, durationSeconds, steps, dateTimeUtc);
    }
}
