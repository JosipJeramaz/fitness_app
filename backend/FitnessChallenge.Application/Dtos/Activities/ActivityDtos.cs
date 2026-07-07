using FitnessChallenge.Domain.Enums;

namespace FitnessChallenge.Application.Dtos.Activities;

/// <summary>
/// Raw ingestion payload. All metric fields are optional; the validation matrix decides which combination
/// is legal. <see cref="Sport"/> being absent (null) is how a Daily Steps activity is expressed.
/// </summary>
public sealed record IngestActivityRequest
{
    public string? UserId { get; init; }
    public string? Datetime { get; init; }
    public string? Sport { get; init; }
    public int? Steps { get; init; }
    public decimal? Distance { get; init; }
    public string? Duration { get; init; }
}

/// <summary>Confirmation returned on successful ingestion.</summary>
public sealed record ActivityIngestResponse(
    string Id,
    string UserId,
    ActivityType ActivityType,
    int Points,
    DateTimeOffset Datetime);

/// <summary>An activity row as served back to the client.</summary>
public sealed record ActivityResponse(
    string Id,
    DateTimeOffset Datetime,
    ActivityType ActivityType,
    decimal? DistanceKm,
    int? DurationSeconds,
    int? Steps,
    int Points);

/// <summary>Allowed sort fields for the serve-activities endpoint (an allowlist — never raw column names).</summary>
public enum ActivitySortField
{
    Datetime = 0,
    Points = 1
}

/// <summary>
/// Filters, sorting and paging for the serve-activities endpoint. Defaults preserve the original
/// contract: newest first, no paging.
/// </summary>
public sealed record ActivityQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    ActivityType? Sport = null,
    ActivitySortField SortBy = ActivitySortField.Datetime,
    bool Ascending = false,
    int? Limit = null,
    int? Offset = null);
