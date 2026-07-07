namespace FitnessChallenge.Application.Dtos.Leaderboard;

/// <summary>
/// A ranked leaderboard row with a snapshot-free trend. <see cref="PreviousRank"/>/<see cref="RankDelta"/>
/// are null for entrants with no activity in the previous window (<see cref="Trend"/> == "new").
/// </summary>
public sealed record LeaderboardEntry(
    int Rank,
    string UserId,
    string FirstName,
    string LastName,
    long TotalPoints,
    int? PreviousRank,
    int? RankDelta,
    string Trend);

/// <summary>
/// Cumulative points race between the top users. Every series is anchored at the user's pre-window
/// total, so the final value of each line equals that user's leaderboard total — the chart and the
/// table always tell the same story.
/// </summary>
public sealed record RaceChartData(IReadOnlyList<string> Dates, IReadOnlyList<RaceSeries> Series);

/// <summary>
/// One user's line: cumulative points aligned index-for-index with <see cref="RaceChartData.Dates"/>.
/// Buckets before the user's first-ever activity are <c>null</c> — the line simply doesn't exist yet,
/// instead of a misleading zero run-up.
/// </summary>
public sealed record RaceSeries(
    string UserId,
    string FirstName,
    string LastName,
    IReadOnlyList<long?> CumulativePoints);
