namespace FitnessChallenge.Application.Abstractions;

/// <summary>
/// Per-user point total and activity count as of a cutoff instant. <see cref="ActivityCount"/> is needed
/// (not just points) to tell "new" entrants apart from users who logged 0-point activities.
/// </summary>
public sealed record UserPointsTotal(
    Guid UserId,
    string FirstName,
    string LastName,
    long TotalPoints,
    int ActivityCount);

/// <summary>A single activity's points at a moment in time (minimal row for time-series aggregation).</summary>
public sealed record ActivityPointRow(Guid UserId, DateTimeOffset DateTimeUtc, int Points);

/// <summary>
/// Read port for leaderboard aggregation. Implemented over the database (GROUP BY) so ranking scales
/// without pulling raw rows into memory. Returns a row for every registered user (including zero-activity).
/// </summary>
public interface ILeaderboardDataProvider
{
    Task<IReadOnlyList<UserPointsTotal>> GetTotalsAsOfAsync(DateTimeOffset asOf, CancellationToken ct);

    /// <summary>
    /// Minimal point rows in the (<paramref name="fromExclusive"/>, <paramref name="toInclusive"/>] window,
    /// used by the race chart. Bounded by the window, so the row count stays small.
    /// </summary>
    Task<IReadOnlyList<ActivityPointRow>> GetPointRowsInRangeAsync(
        DateTimeOffset fromExclusive,
        DateTimeOffset toInclusive,
        CancellationToken ct);

    /// <summary>
    /// The moment of the earliest activity among the given users, or null when they have none.
    /// Anchors the race chart's "since first activity" mode.
    /// </summary>
    Task<DateTimeOffset?> GetEarliestActivityUtcAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}
