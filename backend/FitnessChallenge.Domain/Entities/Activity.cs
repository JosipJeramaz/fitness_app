using FitnessChallenge.Domain.Enums;

namespace FitnessChallenge.Domain.Entities;

/// <summary>
/// A single logged activity. The raw metric (distance/duration/steps) is stored alongside the
/// <see cref="Points"/> computed at ingestion, so leaderboard/dashboard reads are simple sums.
/// Points are an immutable business fact once earned.
/// </summary>
public sealed class Activity
{
    // Required by EF Core materialization.
    private Activity() { }

    private Activity(
        Guid id,
        Guid userId,
        DateTimeOffset dateTimeUtc,
        ActivityType activityType,
        decimal? distanceKm,
        int? durationSeconds,
        int? steps,
        int points,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        DateTimeUtc = dateTimeUtc;
        ActivityType = activityType;
        DistanceKm = distanceKm;
        DurationSeconds = durationSeconds;
        Steps = steps;
        Points = points;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Client-supplied moment of the activity (may be backdated). Stored in UTC.</summary>
    public DateTimeOffset DateTimeUtc { get; private set; }

    public ActivityType ActivityType { get; private set; }

    public decimal? DistanceKm { get; private set; }
    public int? DurationSeconds { get; private set; }
    public int? Steps { get; private set; }

    /// <summary>Normalized, denormalized points computed at ingestion.</summary>
    public int Points { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public User? User { get; private set; }

    /// <summary>
    /// Creates an activity with its already-computed points. Points are calculated by the
    /// scoring service in the application layer and passed in here — the entity never scores itself.
    /// </summary>
    public static Activity Create(
        Guid userId,
        DateTimeOffset dateTimeUtc,
        ActivityType activityType,
        decimal? distanceKm,
        int? durationSeconds,
        int? steps,
        int points,
        DateTimeOffset createdAt,
        Guid? id = null)
        => new(id ?? Guid.NewGuid(), userId, dateTimeUtc, activityType, distanceKm, durationSeconds, steps, points, createdAt);
}
