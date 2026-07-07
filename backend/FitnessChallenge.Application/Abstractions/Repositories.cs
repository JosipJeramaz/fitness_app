using FitnessChallenge.Application.Dtos.Activities;
using FitnessChallenge.Domain.Entities;

namespace FitnessChallenge.Application.Abstractions;

public interface IUserRepository
{
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsByNormalizedNameAsync(string normalizedFirstName, string normalizedLastName, CancellationToken ct);
    Task<bool> ExistsByNormalizedUserNameAsync(string normalizedUserName, CancellationToken ct);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByNormalizedNameAsync(string normalizedFirstName, string normalizedLastName, CancellationToken ct);
    Task<User?> GetByNormalizedUserNameAsync(string normalizedUserName, CancellationToken ct);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
}

/// <summary>One page of a user's activities plus the total row count before paging (same filters).</summary>
public sealed record ActivityPage(IReadOnlyList<Activity> Items, int TotalCount);

public interface IActivityRepository
{
    Task AddAsync(Activity activity, CancellationToken ct);

    Task<Activity?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Stages the activity for physical removal; the unit of work commits it.</summary>
    void Remove(Activity activity);

    /// <summary>
    /// Returns a user's activities filtered, sorted and paged per <paramref name="query"/> — all in SQL —
    /// together with the total count of rows matching the filters (ignoring limit/offset).
    /// </summary>
    Task<ActivityPage> GetForUserAsync(Guid userId, ActivityQuery query, CancellationToken ct);
}

/// <summary>Commit boundary shared by the repositories (the DbContext is the unit of work).</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
