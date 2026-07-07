using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Application.Dtos.Activities;
using FitnessChallenge.Domain.Entities;
using FitnessChallenge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessChallenge.Infrastructure.Persistence;

public sealed class ActivityRepository : IActivityRepository
{
    private readonly AppDbContext _db;

    public ActivityRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Activity activity, CancellationToken ct)
        => await _db.Activities.AddAsync(activity, ct);

    // Tracked (no AsNoTracking) so the entity can be handed straight to Remove.
    public Task<Activity?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Activities.FirstOrDefaultAsync(a => a.Id == id, ct);

    public void Remove(Activity activity) => _db.Activities.Remove(activity);

    public async Task<ActivityPage> GetForUserAsync(Guid userId, ActivityQuery query, CancellationToken ct)
    {
        var filtered = _db.Activities.AsNoTracking().Where(a => a.UserId == userId);

        if (query.From is not null)
            filtered = filtered.Where(a => a.DateTimeUtc >= query.From.Value);
        if (query.To is not null)
            filtered = filtered.Where(a => a.DateTimeUtc <= query.To.Value);
        if (query.Sport is not null)
            filtered = filtered.Where(a => a.ActivityType == query.Sport.Value);

        // Total is counted on the same filtered set, before sort/paging, so "X of N" is always consistent
        // with the filters the client asked for.
        var totalCount = await filtered.CountAsync(ct);

        // Sort in SQL. The trailing Id tie-break makes the order total, which paging requires —
        // otherwise rows with equal keys could repeat or vanish across pages.
        var page = (query.SortBy, query.Ascending) switch
        {
            (ActivitySortField.Points, true) => filtered
                .OrderBy(a => a.Points).ThenBy(a => a.DateTimeUtc).ThenBy(a => a.Id),
            (ActivitySortField.Points, false) => filtered
                .OrderByDescending(a => a.Points).ThenByDescending(a => a.DateTimeUtc).ThenBy(a => a.Id),
            (_, true) => filtered
                .OrderBy(a => a.DateTimeUtc).ThenBy(a => a.Id),
            _ => filtered
                .OrderByDescending(a => a.DateTimeUtc).ThenBy(a => a.Id)
        } as IQueryable<Activity>;

        if (query.Offset is > 0)
            page = page.Skip(query.Offset.Value);
        if (query.Limit is > 0)
            page = page.Take(query.Limit.Value);

        return new ActivityPage(await page.ToListAsync(ct), totalCount);
    }
}
