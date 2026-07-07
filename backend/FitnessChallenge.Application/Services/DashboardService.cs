using System.Globalization;
using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Application.Common;
using FitnessChallenge.Application.Dtos.Activities;
using FitnessChallenge.Application.Dtos.Dashboard;
using FitnessChallenge.Domain.Entities;

namespace FitnessChallenge.Application.Services;

public interface IDashboardService
{
    Task<Result<DashboardData>> GetAsync(Guid userId, string groupBy, CancellationToken ct);
}

/// <summary>
/// Builds the personal dashboard. A single user's activity set is bounded, so aggregation happens in memory
/// after a single indexed read — simple, testable, and avoids provider-specific date-translation quirks.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IUserRepository _users;
    private readonly IActivityRepository _activities;

    public DashboardService(IUserRepository users, IActivityRepository activities)
    {
        _users = users;
        _activities = activities;
    }

    public async Task<Result<DashboardData>> GetAsync(Guid userId, string groupBy, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
            return Error.NotFound("The specified user does not exist.");

        // Aggregation needs the full set; the default query has no paging, so Items is everything.
        var activities = (await _activities.GetForUserAsync(userId, new ActivityQuery(), ct)).Items;

        var byWeek = string.Equals(groupBy, "week", StringComparison.OrdinalIgnoreCase);

        var pointsOverTime = activities
            .GroupBy(a => BucketKey(a.DateTimeUtc, byWeek))
            .OrderBy(g => g.Key)
            .Select(g => new PointsOverTimePoint(
                g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                g.Sum(a => (long)a.Points),
                g.Count()))
            .ToList();

        var sportBreakdown = activities
            .GroupBy(a => a.ActivityType)
            .Select(g => new SportBreakdownItem(g.Key, g.Sum(a => (long)a.Points), g.Count()))
            .OrderByDescending(s => s.Points)
            .ThenBy(s => s.ActivityType.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var data = new DashboardData(
            user.Id.ToString(),
            user.FirstName,
            user.LastName,
            activities.Sum(a => (long)a.Points),
            activities.Count,
            pointsOverTime,
            sportBreakdown);

        return Result<DashboardData>.Success(data);
    }

    private static DateTime BucketKey(DateTimeOffset moment, bool byWeek)
    {
        var date = moment.UtcDateTime.Date;
        if (!byWeek)
            return date;

        // Monday-based week start.
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
