using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessChallenge.Infrastructure.Persistence;

/// <summary>
/// Computes each user's point total and activity count as of a cutoff with a single LEFT JOIN + GROUP BY,
/// so ranking never materializes raw activity rows. Includes every registered user (zero-activity users
/// surface with 0 points / 0 activities).
/// </summary>
public sealed class LeaderboardDataProvider : ILeaderboardDataProvider
{
    private readonly AppDbContext _db;

    public LeaderboardDataProvider(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<UserPointsTotal>> GetTotalsAsOfAsync(DateTimeOffset asOf, CancellationToken ct)
    {
        // Every registered user (so zero-activity users still rank).
        var users = await _db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToListAsync(ct);

        // Aggregate points/count per user in a single GROUP BY over the indexed activity table.
        var aggregates = await _db.Activities.AsNoTracking()
            .Where(a => a.DateTimeUtc <= asOf)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(a => a.Points), Count = g.Count() })
            .ToListAsync(ct);

        var byUser = aggregates.ToDictionary(x => x.UserId);

        return users
            .Select(u =>
            {
                byUser.TryGetValue(u.Id, out var aggregate);
                return new UserPointsTotal(u.Id, u.FirstName, u.LastName, aggregate?.Points ?? 0L, aggregate?.Count ?? 0);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ActivityPointRow>> GetPointRowsInRangeAsync(
        DateTimeOffset fromExclusive,
        DateTimeOffset toInclusive,
        CancellationToken ct)
        => await _db.Activities.AsNoTracking()
            .Where(a => a.DateTimeUtc > fromExclusive && a.DateTimeUtc <= toInclusive)
            .Select(a => new ActivityPointRow(a.UserId, a.DateTimeUtc, a.Points))
            .ToListAsync(ct);

    // ORDER BY + LIMIT 1 instead of Min(): ordering on the ticks-converted column translates cleanly
    // and rides the (UserId, DateTimeUtc) index.
    public async Task<DateTimeOffset?> GetEarliestActivityUtcAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
        => await _db.Activities.AsNoTracking()
            .Where(a => userIds.Contains(a.UserId))
            .OrderBy(a => a.DateTimeUtc)
            .Select(a => (DateTimeOffset?)a.DateTimeUtc)
            .FirstOrDefaultAsync(ct);
}
