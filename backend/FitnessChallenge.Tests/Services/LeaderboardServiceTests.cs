using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Application.Services;
using FitnessChallenge.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace FitnessChallenge.Tests.Services;

/// <summary>
/// Exercises the pure ranking + snapshot-free trend logic with a fake data provider and a fixed clock.
/// </summary>
public class LeaderboardServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-0000000000c3");
    private static readonly Guid D = Guid.Parse("00000000-0000-0000-0000-0000000000d4");

    [Fact]
    public async Task Ranks_by_points_and_reports_every_trend_kind()
    {
        var current = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 1000, 5),
            new UserPointsTotal(B, "Bob", "Brown", 800, 3),
            new UserPointsTotal(C, "Cara", "Clark", 500, 2),
            new UserPointsTotal(D, "Dan", "Davis", 300, 1)
        };
        var previous = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 500, 3),
            new UserPointsTotal(B, "Bob", "Brown", 900, 4),
            new UserPointsTotal(C, "Cara", "Clark", 500, 2),
            new UserPointsTotal(D, "Dan", "Davis", 0, 0) // no activity in the previous window -> bottom of that snapshot
        };
        var sut = new LeaderboardService(new FakeLeaderboardData(Now, current, previous), new FixedTimeProvider(Now));

        var result = (await sut.GetAsync(windowDays: 7, limit: null, offset: null, LeaderboardSortField.Rank, ascending: true, CancellationToken.None)).Items;

        result.Select(r => r.FirstName).Should().ContainInOrder("Ann", "Bob", "Cara", "Dan");
        result.Single(r => r.UserId == A.ToString()).Should().BeEquivalentTo(new { Rank = 1, PreviousRank = 2, RankDelta = 1, Trend = "up" });
        result.Single(r => r.UserId == B.ToString()).Should().BeEquivalentTo(new { Rank = 2, PreviousRank = 1, RankDelta = -1, Trend = "down" });
        result.Single(r => r.UserId == C.ToString()).Should().BeEquivalentTo(new { Rank = 3, PreviousRank = 3, RankDelta = 0, Trend = "same" });
        result.Single(r => r.UserId == D.ToString()).Should().BeEquivalentTo(new { Rank = 4, PreviousRank = 4, RankDelta = 0, Trend = "same" });
    }

    [Fact]
    public async Task Breaks_ties_by_last_then_first_name()
    {
        // Same points -> deterministic order by last name, then first name.
        var totals = new[]
        {
            new UserPointsTotal(A, "Zoe", "Adams", 500, 1),
            new UserPointsTotal(B, "Amy", "Brown", 500, 1),
            new UserPointsTotal(C, "Bea", "Adams", 500, 1)
        };
        var sut = new LeaderboardService(new FakeLeaderboardData(Now, totals, totals), new FixedTimeProvider(Now));

        var result = (await sut.GetAsync(7, null, null, LeaderboardSortField.Rank, true, CancellationToken.None)).Items;

        // Adams before Brown; within Adams, "Bea" before "Zoe".
        result.Select(r => r.FirstName).Should().ContainInOrder("Bea", "Zoe", "Amy");
        result.Select(r => r.Rank).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public async Task Applies_limit_and_offset()
    {
        var totals = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 400, 1),
            new UserPointsTotal(B, "Bob", "Brown", 300, 1),
            new UserPointsTotal(C, "Cara", "Clark", 200, 1),
            new UserPointsTotal(D, "Dan", "Davis", 100, 1)
        };
        var sut = new LeaderboardService(new FakeLeaderboardData(Now, totals, totals), new FixedTimeProvider(Now));

        var page = await sut.GetAsync(7, limit: 2, offset: 1, LeaderboardSortField.Rank, true, CancellationToken.None);

        page.Items.Should().HaveCount(2);
        page.TotalCount.Should().Be(4); // pre-paging size, drives the client paginator
        page.Items.Select(r => r.FirstName).Should().ContainInOrder("Bob", "Cara");
        page.Items.First().Rank.Should().Be(2); // ranks are absolute, not per-page
    }

    [Fact]
    public async Task Sorts_by_trend_hierarchy_with_rank_tiebreak()
    {
        // Current ranking: Ann(1, up), Bob(2, down), Cara(3, same), Dan(4, same — bottom then, bottom now).
        var current = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 1000, 5),
            new UserPointsTotal(B, "Bob", "Brown", 800, 3),
            new UserPointsTotal(C, "Cara", "Clark", 500, 2),
            new UserPointsTotal(D, "Dan", "Davis", 300, 1)
        };
        var previous = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 500, 3),
            new UserPointsTotal(B, "Bob", "Brown", 900, 4),
            new UserPointsTotal(C, "Cara", "Clark", 500, 2),
            new UserPointsTotal(D, "Dan", "Davis", 0, 0)
        };
        var sut = new LeaderboardService(new FakeLeaderboardData(Now, current, previous), new FixedTimeProvider(Now));

        var ascending = (await sut.GetAsync(7, null, null, LeaderboardSortField.Trend, true, CancellationToken.None)).Items;
        ascending.Select(r => r.Trend).Should().ContainInOrder("up", "same", "same", "down");
        ascending.Select(r => r.FirstName).Should().ContainInOrder("Ann", "Cara", "Dan", "Bob"); // rank tie-break inside "same"

        var descending = (await sut.GetAsync(7, null, null, LeaderboardSortField.Trend, false, CancellationToken.None)).Items;
        descending.Select(r => r.Trend).Should().ContainInOrder("down", "same", "same", "up");

        // Global ranks are untouched by the ordering.
        ascending.Single(r => r.Trend == "up").Rank.Should().Be(1);
    }

    [Fact]
    public async Task New_entrant_with_points_trends_up_from_the_bottom_of_the_previous_board()
    {
        // Dan had no activity before the window: he ranked last (0 points) in the previous snapshot,
        // so entering the board mid-pack is a genuine climb, not a special "new" state.
        var current = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 600, 4),
            new UserPointsTotal(D, "Dan", "Davis", 500, 2),
            new UserPointsTotal(B, "Bob", "Brown", 400, 3)
        };
        var previous = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 500, 3),
            new UserPointsTotal(B, "Bob", "Brown", 400, 3),
            new UserPointsTotal(D, "Dan", "Davis", 0, 0)
        };
        var sut = new LeaderboardService(new FakeLeaderboardData(Now, current, previous), new FixedTimeProvider(Now));

        var items = (await sut.GetAsync(7, null, null, LeaderboardSortField.Rank, true, CancellationToken.None)).Items;

        var dan = items.Single(r => r.FirstName == "Dan");
        dan.Rank.Should().Be(2);
        dan.PreviousRank.Should().Be(3);
        dan.RankDelta.Should().Be(1);
        dan.Trend.Should().Be("up");

        var bob = items.Single(r => r.FirstName == "Bob");
        bob.Trend.Should().Be("down"); // pushed down by the new entrant
    }

    [Fact]
    public async Task Rank_sort_descending_reverses_the_ranking()
    {
        var totals = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 400, 1),
            new UserPointsTotal(B, "Bob", "Brown", 300, 1),
            new UserPointsTotal(C, "Cara", "Clark", 200, 1)
        };
        var sut = new LeaderboardService(new FakeLeaderboardData(Now, totals, totals), new FixedTimeProvider(Now));

        var result = (await sut.GetAsync(7, null, null, LeaderboardSortField.Rank, false, CancellationToken.None)).Items;

        result.Select(r => r.Rank).Should().ContainInOrder(3, 2, 1);
    }

    [Fact]
    public async Task Race_chart_anchors_lines_at_baseline_and_ends_at_leaderboard_totals()
    {
        // Window: 3 days back from Now (2026-07-01 12:00) -> buckets 06-28 .. 07-01 (4 days).
        var current = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 150, 2),
            new UserPointsTotal(B, "Bob", "Brown", 500, 1)
        };
        var baseline = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 100, 1), // 100 pts earned before the window
            new UserPointsTotal(B, "Bob", "Brown", 0, 0)
        };
        var rows = new[]
        {
            new ActivityPointRow(B, new DateTimeOffset(2026, 6, 29, 9, 0, 0, TimeSpan.Zero), 500),
            new ActivityPointRow(A, new DateTimeOffset(2026, 6, 30, 10, 0, 0, TimeSpan.Zero), 50)
        };
        var sut = new LeaderboardService(new FakeLeaderboardData(Now, current, baseline, rows), new FixedTimeProvider(Now));

        var race = await sut.GetRaceAsync(days: 3, top: 5, CancellationToken.None);

        race.Dates.Should().ContainInOrder("2026-06-28", "2026-06-29", "2026-06-30", "2026-07-01");

        race.Series.Should().HaveCount(2);
        race.Series[0].FirstName.Should().Be("Bob"); // ordered by current rank
        // Bob's first-ever activity is 06-29 -> no line before it (null), then his cumulative values.
        race.Series[0].CumulativePoints.Should().Equal(null, 500L, 500L, 500L);
        // Ann was active before the window -> her line starts immediately, anchored at the baseline.
        race.Series[1].CumulativePoints.Should().Equal(100L, 100L, 150L, 150L);

        // The invariant that keeps the chart and the table consistent:
        race.Series.Select(s => s.CumulativePoints[^1])
            .Should().BeEquivalentTo(new long?[] { 500L, 150L });
    }

    [Fact]
    public async Task Race_chart_since_first_activity_starts_at_the_earliest_activity_day()
    {
        var current = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 150, 2),
            new UserPointsTotal(B, "Bob", "Brown", 500, 1)
        };
        var baseline = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 0, 0),
            new UserPointsTotal(B, "Bob", "Brown", 0, 0)
        };
        var rows = new[]
        {
            new ActivityPointRow(B, new DateTimeOffset(2026, 6, 29, 9, 0, 0, TimeSpan.Zero), 500),
            new ActivityPointRow(A, new DateTimeOffset(2026, 6, 30, 10, 0, 0, TimeSpan.Zero), 150)
        };
        var sut = new LeaderboardService(new FakeLeaderboardData(Now, current, baseline, rows), new FixedTimeProvider(Now));

        var race = await sut.GetRaceAsync(days: null, top: 5, CancellationToken.None);

        // One lead-in day of axis context before the earliest charted activity (06-29), but no zero
        // run-up: each line is null (not drawn) until that athlete's own first activity.
        race.Dates.Should().ContainInOrder("2026-06-28", "2026-06-29", "2026-06-30", "2026-07-01");
        race.Dates.Should().HaveCount(4);
        race.Series[0].CumulativePoints.Should().Equal(null, 500L, 500L, 500L);  // Bob enters 06-29
        race.Series[1].CumulativePoints.Should().Equal(null, null, 150L, 150L);  // Ann enters 06-30
        race.Series.Select(s => s.CumulativePoints[^1]).Should().BeEquivalentTo(new long?[] { 500L, 150L });
    }

    [Fact]
    public async Task Race_chart_limits_series_to_top_n()
    {
        var totals = new[]
        {
            new UserPointsTotal(A, "Ann", "Adams", 400, 1),
            new UserPointsTotal(B, "Bob", "Brown", 300, 1),
            new UserPointsTotal(C, "Cara", "Clark", 200, 1)
        };
        var sut = new LeaderboardService(new FakeLeaderboardData(Now, totals, totals), new FixedTimeProvider(Now));

        var race = await sut.GetRaceAsync(days: 7, top: 2, CancellationToken.None);

        race.Series.Select(s => s.FirstName).Should().ContainInOrder("Ann", "Bob");
    }

    private sealed class FakeLeaderboardData : ILeaderboardDataProvider
    {
        private readonly DateTimeOffset _now;
        private readonly IReadOnlyList<UserPointsTotal> _current;
        private readonly IReadOnlyList<UserPointsTotal> _previous;
        private readonly IReadOnlyList<ActivityPointRow> _rows;

        public FakeLeaderboardData(
            DateTimeOffset now,
            IReadOnlyList<UserPointsTotal> current,
            IReadOnlyList<UserPointsTotal> previous,
            IReadOnlyList<ActivityPointRow>? rows = null)
        {
            _now = now;
            _current = current;
            _previous = previous;
            _rows = rows ?? Array.Empty<ActivityPointRow>();
        }

        public Task<IReadOnlyList<UserPointsTotal>> GetTotalsAsOfAsync(DateTimeOffset asOf, CancellationToken ct)
            => Task.FromResult(asOf >= _now ? _current : _previous);

        public Task<IReadOnlyList<ActivityPointRow>> GetPointRowsInRangeAsync(DateTimeOffset fromExclusive, DateTimeOffset toInclusive, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ActivityPointRow>>(
                _rows.Where(r => r.DateTimeUtc > fromExclusive && r.DateTimeUtc <= toInclusive).ToList());

        public Task<DateTimeOffset?> GetEarliestActivityUtcAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
        {
            var matching = _rows.Where(r => userIds.Contains(r.UserId)).ToList();
            return Task.FromResult<DateTimeOffset?>(matching.Count == 0 ? null : matching.Min(r => r.DateTimeUtc));
        }
    }
}
