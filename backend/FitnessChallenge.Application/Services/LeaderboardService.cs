using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Application.Common;
using FitnessChallenge.Application.Dtos.Leaderboard;

namespace FitnessChallenge.Application.Services;

/// <summary>Leaderboard orderings: the default rank order, or grouping by trend category.</summary>
public enum LeaderboardSortField
{
    Rank,
    Trend
}

public interface ILeaderboardService
{
    Task<PagedResult<LeaderboardEntry>> GetAsync(
        int windowDays, int? limit, int? offset, LeaderboardSortField sortBy, bool ascending, CancellationToken ct);

    /// <summary><paramref name="days"/> = null means "since the first activity of the charted users".</summary>
    Task<RaceChartData> GetRaceAsync(int? days, int top, CancellationToken ct);
}

/// <summary>
/// Ranks users by total points and derives a snapshot-free trend by comparing the current ranking with the
/// ranking as of <c>now - window</c>. All ranking/trend math is pure given the data provider, so it is
/// deterministic and unit-testable with a fake provider and a fixed clock.
/// </summary>
public sealed class LeaderboardService : ILeaderboardService
{
    private const int DefaultWindowDays = 7;
    private const int DefaultRaceDays = 30;
    private const int DefaultRaceTop = 5;

    private readonly ILeaderboardDataProvider _data;
    private readonly TimeProvider _clock;

    public LeaderboardService(ILeaderboardDataProvider data, TimeProvider clock)
    {
        _data = data;
        _clock = clock;
    }

    public async Task<PagedResult<LeaderboardEntry>> GetAsync(
        int windowDays, int? limit, int? offset, LeaderboardSortField sortBy, bool ascending, CancellationToken ct)
    {
        if (windowDays < 0)
            windowDays = DefaultWindowDays;

        var now = _clock.GetUtcNow();
        var previousCutoff = now - TimeSpan.FromDays(windowDays);

        var current = await _data.GetTotalsAsOfAsync(now, ct);
        var previous = await _data.GetTotalsAsOfAsync(previousCutoff, ct);

        var currentRanked = AssignRanks(current);
        var previousRankByUser = AssignRanks(previous).ToDictionary(x => x.User.UserId, x => x.Rank);

        var entries = new List<LeaderboardEntry>(currentRanked.Count);
        foreach (var (user, rank) in currentRanked)
        {
            // Zero-activity users rank at the bottom of the previous snapshot too, so a brand-new
            // competitor gets a genuine "climbed from the bottom" trend instead of a special state.
            var previousRank = previousRankByUser.TryGetValue(user.UserId, out var pr) ? pr : previous.Count + 1;
            var rankDelta = previousRank - rank; // positive => moved up
            var trend = rankDelta > 0 ? "up" : rankDelta < 0 ? "down" : "same";

            entries.Add(new LeaderboardEntry(
                rank,
                user.UserId.ToString(),
                user.FirstName,
                user.LastName,
                user.TotalPoints,
                previousRank,
                rankDelta,
                trend));
        }

        IEnumerable<LeaderboardEntry> page = Sort(entries, sortBy, ascending);
        if (offset is > 0)
            page = page.Skip(offset.Value);
        if (limit is > 0)
            page = page.Take(limit.Value);

        // Ranks are assigned before sorting/paging, so entry.Rank stays global; TotalCount is the pre-paging size.
        return new PagedResult<LeaderboardEntry>(page.ToList(), entries.Count);
    }

    /// <summary>
    /// Trend sort groups by category — ascending follows the hierarchy up → same → down
    /// (descending reverses it) — with rank as the stable tie-break inside each group.
    /// </summary>
    private static IEnumerable<LeaderboardEntry> Sort(List<LeaderboardEntry> entries, LeaderboardSortField sortBy, bool ascending)
    {
        if (sortBy == LeaderboardSortField.Rank)
            return ascending ? entries : Enumerable.Reverse(entries); // already ordered by rank

        var byTrend = ascending
            ? entries.OrderBy(e => TrendOrder(e.Trend))
            : entries.OrderByDescending(e => TrendOrder(e.Trend));
        return byTrend.ThenBy(e => e.Rank);
    }

    private static int TrendOrder(string trend) => trend switch
    {
        "up" => 0,
        "same" => 1,
        _ => 2 // "down" — losing ground sorts last
    };

    /// <summary>
    /// Cumulative points race for the current top <paramref name="top"/> users. With a fixed
    /// <paramref name="days"/> window, each line starts at the user's pre-window total, so its final value
    /// equals the leaderboard total — the chart never contradicts the table. With <paramref name="days"/>
    /// = null, the window starts at the charted users' first activity (no empty lead-in), capped at a year.
    /// </summary>
    public async Task<RaceChartData> GetRaceAsync(int? days, int top, CancellationToken ct)
    {
        top = Math.Clamp(top is < 1 ? DefaultRaceTop : top, 1, 20);

        var now = _clock.GetUtcNow();

        // Top users by the same ranking the table uses, so the chart shows exactly the leaders above it.
        var topUsers = AssignRanks(await _data.GetTotalsAsOfAsync(now, ct))
            .Take(top)
            .Select(r => r.User)
            .ToList();
        var topIds = topUsers.Select(u => u.UserId).ToHashSet();

        DateTimeOffset windowStart;
        DateTime firstDay;
        if (days is null)
        {
            // "Since first activity": anchor at the earliest activity of the charted users (1 tick before,
            // so that activity falls INSIDE the window and the baseline is empty), capped at a year back.
            var earliest = await _data.GetEarliestActivityUtcAsync(topIds, ct) ?? now;
            var cap = now - TimeSpan.FromDays(365);
            if (earliest > cap)
            {
                windowStart = earliest.AddTicks(-1);
                // One lead-in day so every line visibly rises from its baseline on the first scoring day,
                // instead of starting mid-air at an already-positive value.
                firstDay = earliest.UtcDateTime.Date.AddDays(-1);
            }
            else
            {
                windowStart = cap;
                firstDay = cap.UtcDateTime.Date;
            }
        }
        else
        {
            var clamped = Math.Clamp(days.Value is < 1 ? DefaultRaceDays : days.Value, 1, 365);
            windowStart = now - TimeSpan.FromDays(clamped);
            firstDay = windowStart.UtcDateTime.Date;
        }

        // Rows at or before windowStart are covered by the baseline; the range query is exclusive below.
        var baseline = (await _data.GetTotalsAsOfAsync(windowStart, ct)).ToDictionary(t => t.UserId);
        var rows = await _data.GetPointRowsInRangeAsync(windowStart, now, ct);
        var dayCount = (int)(now.UtcDateTime.Date - firstDay).TotalDays + 1;
        var dayBuckets = Enumerable.Range(0, dayCount).Select(i => firstDay.AddDays(i)).ToArray();

        var pointsPerUserDay = rows
            .Where(r => topIds.Contains(r.UserId))
            .GroupBy(r => (r.UserId, Day: r.DateTimeUtc.UtcDateTime.Date))
            .ToDictionary(g => g.Key, g => g.Sum(r => (long)r.Points));

        var series = topUsers
            .Select(user =>
            {
                baseline.TryGetValue(user.UserId, out var basis);

                // A line exists only from the user's first-ever activity onwards: pre-window activity
                // (baseline count > 0) means it starts on the first bucket at the baseline value;
                // otherwise buckets stay null until the user's first in-window scoring day.
                var started = basis is { ActivityCount: > 0 };
                var running = basis?.TotalPoints ?? 0L;

                var cumulative = new long?[dayCount];
                for (var i = 0; i < dayCount; i++)
                {
                    if (pointsPerUserDay.TryGetValue((user.UserId, dayBuckets[i]), out var earned))
                    {
                        running += earned;
                        started = true;
                    }
                    cumulative[i] = started ? running : null;
                }
                return new RaceSeries(user.UserId.ToString(), user.FirstName, user.LastName, cumulative);
            })
            .ToList();

        var dates = dayBuckets
            .Select(d => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        return new RaceChartData(dates, series);
    }

    /// <summary>
    /// Deterministic total order: points desc, then last name, first name (case-insensitive) asc, then id.
    /// Ranks are unique and sequential (1..n).
    /// </summary>
    private static List<(UserPointsTotal User, int Rank)> AssignRanks(IReadOnlyList<UserPointsTotal> totals)
    {
        var ordered = totals
            .OrderByDescending(t => t.TotalPoints)
            .ThenBy(t => t.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.UserId)
            .ToList();

        var ranked = new List<(UserPointsTotal, int)>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
            ranked.Add((ordered[i], i + 1));

        return ranked;
    }
}
