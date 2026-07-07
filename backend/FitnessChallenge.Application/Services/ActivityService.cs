using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Application.Activities;
using FitnessChallenge.Application.Common;
using FitnessChallenge.Application.Dtos.Activities;
using FitnessChallenge.Domain.Entities;
using FitnessChallenge.Domain.Scoring;

namespace FitnessChallenge.Application.Services;

public interface IActivityService
{
    Task<Result<ActivityIngestResponse>> IngestAsync(IngestActivityRequest request, CancellationToken ct);
    Task<Result<PagedResult<ActivityResponse>>> GetForUserAsync(Guid userId, ActivityQuery query, CancellationToken ct);

    /// <summary>
    /// Hard-deletes one activity. When <paramref name="requiredOwnerId"/> is provided the activity must
    /// belong to that user (403 otherwise); null means auth is disabled and ownership is not enforced.
    /// </summary>
    Task<Result<bool>> DeleteAsync(Guid activityId, Guid? requiredOwnerId, CancellationToken ct);
}

public sealed class ActivityService : IActivityService
{
    private readonly IUserRepository _users;
    private readonly IActivityRepository _activities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IScoringService _scoring;
    private readonly TimeProvider _clock;

    public ActivityService(
        IUserRepository users,
        IActivityRepository activities,
        IUnitOfWork unitOfWork,
        IScoringService scoring,
        TimeProvider clock)
    {
        _users = users;
        _activities = activities;
        _unitOfWork = unitOfWork;
        _scoring = scoring;
        _clock = clock;
    }

    public async Task<Result<ActivityIngestResponse>> IngestAsync(IngestActivityRequest request, CancellationToken ct)
    {
        // Matrix validation (also runs in the controller's FluentValidation pass; re-checked here defensively).
        var interpretation = ActivityInterpreter.Interpret(request, _clock.GetUtcNow());
        if (!interpretation.IsValid)
        {
            var first = interpretation.Errors[0];
            return Error.Validation(first.Message, first.Field);
        }

        if (!Guid.TryParse(request.UserId, out var userId))
            return Error.Validation("'userId' must be a valid identifier.", "userId");

        // Unknown user is reported as a 400 validation error (keeps every invalid ingest at 400 per the spec).
        if (!await _users.ExistsAsync(userId, ct))
            return Error.Validation("The specified user does not exist.", "userId");

        var points = _scoring.CalculatePoints(
            interpretation.Type,
            interpretation.DistanceKm,
            interpretation.DurationSeconds,
            interpretation.Steps);

        var activity = Activity.Create(
            userId,
            interpretation.DateTimeUtc,
            interpretation.Type,
            interpretation.DistanceKm,
            interpretation.DurationSeconds,
            interpretation.Steps,
            points,
            _clock.GetUtcNow());

        await _activities.AddAsync(activity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ActivityIngestResponse>.Success(new ActivityIngestResponse(
            activity.Id.ToString(),
            userId.ToString(),
            activity.ActivityType,
            points,
            activity.DateTimeUtc));
    }

    public async Task<Result<PagedResult<ActivityResponse>>> GetForUserAsync(Guid userId, ActivityQuery query, CancellationToken ct)
    {
        if (!await _users.ExistsAsync(userId, ct))
            return Error.NotFound("The specified user does not exist.");

        var page = await _activities.GetForUserAsync(userId, query, ct);

        var items = page.Items
            .Select(a => new ActivityResponse(
                a.Id.ToString(),
                a.DateTimeUtc,
                a.ActivityType,
                a.DistanceKm,
                a.DurationSeconds,
                a.Steps,
                a.Points))
            .ToList();

        return Result<PagedResult<ActivityResponse>>.Success(new PagedResult<ActivityResponse>(items, page.TotalCount));
    }

    public async Task<Result<bool>> DeleteAsync(Guid activityId, Guid? requiredOwnerId, CancellationToken ct)
    {
        var activity = await _activities.GetByIdAsync(activityId, ct);
        if (activity is null)
            return Error.NotFound("The specified activity does not exist.");

        // Null owner means auth is disabled — ownership is not enforced (mirrors the ingest gate).
        if (requiredOwnerId is not null && activity.UserId != requiredOwnerId.Value)
            return Error.Forbidden("You can only delete your own activities.");

        // Points are denormalized per row and all aggregates are computed at read time,
        // so removing the row is automatically consistent everywhere — nothing to recalculate.
        _activities.Remove(activity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
