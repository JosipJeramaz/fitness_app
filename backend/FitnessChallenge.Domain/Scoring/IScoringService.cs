using FitnessChallenge.Domain.Enums;

namespace FitnessChallenge.Domain.Scoring;

/// <summary>
/// Normalizes any activity into integer Points. Pure and dependency-free by design so it is
/// trivially unit-testable and deterministic.
/// </summary>
public interface IScoringService
{
    /// <summary>
    /// Computes points for a validated activity. Exactly one metric is expected to be non-null,
    /// matching <paramref name="type"/>; the others are ignored.
    /// </summary>
    int CalculatePoints(ActivityType type, decimal? distanceKm, int? durationSeconds, int? steps);
}
