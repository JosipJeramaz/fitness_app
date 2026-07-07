using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Application.Common;
using FitnessChallenge.Application.Dtos.Activities;
using FitnessChallenge.Application.Services;
using FitnessChallenge.Domain.Entities;
using FitnessChallenge.Domain.Enums;
using FitnessChallenge.Domain.Scoring;
using FitnessChallenge.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace FitnessChallenge.Tests.Services;

public class ActivityServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Delete_removes_the_activity_and_commits()
    {
        var ownerId = Guid.NewGuid();
        var activity = MakeActivity(ownerId);
        var repo = new StubActivityRepository(activity);
        var uow = new RecordingUnitOfWork();
        var sut = CreateSut(repo, uow);

        var result = await sut.DeleteAsync(activity.Id, ownerId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repo.Activities.Should().NotContain(activity);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Delete_unknown_activity_returns_not_found()
    {
        var repo = new StubActivityRepository();
        var uow = new RecordingUnitOfWork();
        var sut = CreateSut(repo, uow);

        var result = await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_of_another_users_activity_is_forbidden()
    {
        var activity = MakeActivity(Guid.NewGuid());
        var repo = new StubActivityRepository(activity);
        var uow = new RecordingUnitOfWork();
        var sut = CreateSut(repo, uow);

        var result = await sut.DeleteAsync(activity.Id, requiredOwnerId: Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        repo.Activities.Should().Contain(activity);
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_skips_ownership_when_no_owner_required()
    {
        // requiredOwnerId = null models the auth-disabled deployment: anyone may delete (mirrors ingest).
        var activity = MakeActivity(Guid.NewGuid());
        var repo = new StubActivityRepository(activity);
        var uow = new RecordingUnitOfWork();
        var sut = CreateSut(repo, uow);

        var result = await sut.DeleteAsync(activity.Id, requiredOwnerId: null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repo.Activities.Should().BeEmpty();
        uow.SaveCount.Should().Be(1);
    }

    private static ActivityService CreateSut(IActivityRepository activities, IUnitOfWork uow)
        => new(new StubUserRepository(), activities, uow, new ScoringService(), new FixedTimeProvider(Now));

    private static Activity MakeActivity(Guid userId)
        => Activity.Create(userId, Now, ActivityType.Running, 5.0m, null, null, points: 500, Now);

    private sealed class StubActivityRepository : IActivityRepository
    {
        public List<Activity> Activities { get; }
        public StubActivityRepository(params Activity[] activities) => Activities = activities.ToList();

        public Task AddAsync(Activity activity, CancellationToken ct)
        {
            Activities.Add(activity);
            return Task.CompletedTask;
        }

        public Task<Activity?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Activities.FirstOrDefault(a => a.Id == id));

        public void Remove(Activity activity) => Activities.Remove(activity);

        public Task<ActivityPage> GetForUserAsync(Guid userId, ActivityQuery query, CancellationToken ct)
        {
            var items = Activities.Where(a => a.UserId == userId).ToList();
            return Task.FromResult(new ActivityPage(items, items.Count));
        }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    // The delete path never touches users; every member simply answers "no such user".
    private sealed class StubUserRepository : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> ExistsByNormalizedNameAsync(string f, string l, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> ExistsByNormalizedUserNameAsync(string u, CancellationToken ct) => Task.FromResult(false);
        public Task<User?> GetByNormalizedNameAsync(string f, string l, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<User?> GetByNormalizedUserNameAsync(string u, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<User>>(Array.Empty<User>());
        public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask;
    }
}
