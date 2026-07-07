using FitnessChallenge.Domain.Enums;

namespace FitnessChallenge.Domain.Scoring;

/// <summary>
/// Reference implementation of the scoring rules. The order of operations differs per family and
/// is the crux of correctness:
/// <list type="bullet">
///   <item>Distance sports: multiply then floor the final value.</item>
///   <item>Duration sports: floor to whole completed minutes first, then multiply.</item>
///   <item>Steps: floor to whole completed blocks of 100 first.</item>
/// </list>
/// All distance math uses <see cref="decimal"/> to avoid binary floating-point drift
/// (e.g. <c>0.29 * 100</c> must be 29, not 28.999...).
/// </summary>
public sealed class ScoringService : IScoringService
{
    // Points per kilometre (distance sports).
    private const decimal RunningPointsPerKm = 100m;
    private const decimal WalkingPointsPerKm = 50m;
    private const decimal CyclingPointsPerKm = 25m;

    // Points per fully completed minute (duration sports).
    private const int SwimmingPointsPerMinute = 15;
    private const int GymPointsPerMinute = 5;

    // Steps required to earn a single point.
    private const int StepsPerPoint = 100;

    private const int SecondsPerMinute = 60;

    public int CalculatePoints(ActivityType type, decimal? distanceKm, int? durationSeconds, int? steps) => type switch
    {
        ActivityType.Running => DistancePoints(distanceKm, RunningPointsPerKm),
        ActivityType.Walking => DistancePoints(distanceKm, WalkingPointsPerKm),
        ActivityType.Cycling => DistancePoints(distanceKm, CyclingPointsPerKm),
        ActivityType.Swimming => DurationPoints(durationSeconds, SwimmingPointsPerMinute),
        ActivityType.Gym => DurationPoints(durationSeconds, GymPointsPerMinute),
        ActivityType.DailySteps => StepPoints(steps),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown activity type.")
    };

    // Multiply, then floor the final decimal value.
    private static int DistancePoints(decimal? distanceKm, decimal pointsPerKm)
    {
        var km = Require(distanceKm, "distance");
        return (int)Math.Floor(km * pointsPerKm);
    }

    // Floor to whole completed minutes first (integer division), then multiply.
    private static int DurationPoints(int? durationSeconds, int pointsPerMinute)
    {
        var seconds = Require(durationSeconds, "duration");
        var completedMinutes = seconds / SecondsPerMinute;
        return completedMinutes * pointsPerMinute;
    }

    // Floor to whole completed blocks of 100 first (integer division by 100).
    private static int StepPoints(int? steps)
    {
        var count = Require(steps, "steps");
        return count / StepsPerPoint;
    }

    private static T Require<T>(T? value, string metric) where T : struct
        => value ?? throw new ArgumentNullException(metric, $"'{metric}' is required for this activity type.");
}
