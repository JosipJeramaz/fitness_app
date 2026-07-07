using FitnessChallenge.Domain.Enums;

namespace FitnessChallenge.Application.Dtos.Dashboard;

/// <summary>Everything the personal dashboard needs, aggregated server-side.</summary>
public sealed record DashboardData(
    string UserId,
    string FirstName,
    string LastName,
    long TotalPoints,
    int ActivityCount,
    IReadOnlyList<PointsOverTimePoint> PointsOverTime,
    IReadOnlyList<SportBreakdownItem> SportBreakdown);

/// <summary>One time bucket (day or week start) for the activity-volume chart.</summary>
public sealed record PointsOverTimePoint(string Date, long Points, int ActivityCount);

/// <summary>Totals per activity type for the preferred-sports chart.</summary>
public sealed record SportBreakdownItem(ActivityType ActivityType, long Points, int ActivityCount);
