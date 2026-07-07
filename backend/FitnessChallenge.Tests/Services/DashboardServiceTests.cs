using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Application.Common;
using FitnessChallenge.Application.Dtos.Activities;
using FitnessChallenge.Application.Services;
using FitnessChallenge.Domain.Entities;
using FitnessChallenge.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FitnessChallenge.Tests.Services;

public class DashboardServiceTests
{
    [Fact]
    public async Task Aggregates_totals_daily_buckets_and_sport_breakdown()
    {
        var user = User.Register("Ada", "Lovelace", DateTimeOffset.UnixEpoch);
        var activities = new[]
        {
            Activity.Create(user.Id, Day(28, 10), ActivityType.Running, 3.0m, null, null, points: 300, Day(28, 10)),
            Activity.Create(user.Id, Day(28, 18), ActivityType.Swimming, null, 1200, null, points: 100, Day(28, 18)),
            Activity.Create(user.Id, Day(29, 9), ActivityType.DailySteps, null, null, 5000, points: 50, Day(29, 9)),
        };
        var sut = new DashboardService(new StubUserRepository(user), new StubActivityRepository(activities));

        var result = await sut.GetAsync(user.Id, "day", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var data = result.Value!;
        data.TotalPoints.Should().Be(450);
        data.ActivityCount.Should().Be(3);

        data.PointsOverTime.Should().HaveCount(2);
        data.PointsOverTime[0].Should().BeEquivalentTo(new { Date = "2026-06-28", Points = 400L, ActivityCount = 2 });
        data.PointsOverTime[1].Should().BeEquivalentTo(new { Date = "2026-06-29", Points = 50L, ActivityCount = 1 });

        data.SportBreakdown.Select(s => s.ActivityType)
            .Should().ContainInOrder(ActivityType.Running, ActivityType.Swimming, ActivityType.DailySteps); // by points desc
    }

    [Fact]
    public async Task Unknown_user_returns_not_found()
    {
        var sut = new DashboardService(new StubUserRepository(null), new StubActivityRepository(Array.Empty<Activity>()));

        var result = await sut.GetAsync(Guid.NewGuid(), "day", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    private static DateTimeOffset Day(int day, int hour) => new(2026, 6, day, hour, 0, 0, TimeSpan.Zero);

    private sealed class StubUserRepository : IUserRepository
    {
        private readonly User? _user;
        public StubUserRepository(User? user) => _user = user;

        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_user is not null && _user.Id == id ? _user : null);
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct) => Task.FromResult(_user is not null && _user.Id == id);
        public Task<bool> ExistsByNormalizedNameAsync(string f, string l, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> ExistsByNormalizedUserNameAsync(string u, CancellationToken ct) => Task.FromResult(false);
        public Task<User?> GetByNormalizedNameAsync(string f, string l, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<User?> GetByNormalizedUserNameAsync(string u, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<User>>(_user is null ? Array.Empty<User>() : new[] { _user });
        public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StubActivityRepository : IActivityRepository
    {
        private readonly IReadOnlyList<Activity> _activities;
        public StubActivityRepository(IReadOnlyList<Activity> activities) => _activities = activities;

        public Task AddAsync(Activity activity, CancellationToken ct) => Task.CompletedTask;

        public Task<Activity?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_activities.FirstOrDefault(a => a.Id == id));

        public void Remove(Activity activity) { }

        public Task<ActivityPage> GetForUserAsync(Guid userId, ActivityQuery query, CancellationToken ct)
        {
            var items = _activities.Where(a => a.UserId == userId).ToList();
            return Task.FromResult(new ActivityPage(items, items.Count));
        }
    }
}
