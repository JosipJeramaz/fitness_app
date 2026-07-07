using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FitnessChallenge.Api.Common;
using FitnessChallenge.Application.Common;
using FitnessChallenge.Application.Dtos.Activities;
using FitnessChallenge.Application.Services;
using FitnessChallenge.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FitnessChallenge.Api.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class ActivitiesController : ControllerBase
{
    private readonly IActivityService _activities;
    private readonly JwtOptions _authOptions;

    public ActivitiesController(IActivityService activities, IOptions<JwtOptions> authOptions)
    {
        _activities = activities;
        _authOptions = authOptions.Value;
    }

    /// <summary>Ingests one activity: validates the metric matrix, computes points, and stores it.</summary>
    [HttpPost("/api/activities")]
    [ProducesResponseType(typeof(ActivityIngestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest([FromBody] IngestActivityRequest request, CancellationToken ct)
    {
        // Ownership enforcement is only active when the JWT feature is enabled (core stays open otherwise).
        if (_authOptions.Enabled)
        {
            if (User.Identity?.IsAuthenticated != true)
                return ErrorResults.ToActionResult(Error.Unauthorized("Authentication is required."));

            var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.Equals(subject, request.UserId, StringComparison.OrdinalIgnoreCase))
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = "You can only log activities for yourself." });
        }

        var result = await _activities.IngestAsync(request, ct);
        if (!result.IsSuccess)
            return ErrorResults.ToActionResult(result.Error!);

        return CreatedAtAction(nameof(GetForUser), new { userId = result.Value!.UserId }, result.Value);
    }

    /// <summary>
    /// Hard-deletes one activity. Owner-only when auth is enabled; aggregates are computed at
    /// read time, so no recalculation is needed after removal.
    /// </summary>
    [HttpDelete("/api/activities/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var activityId))
            return ErrorResults.ToActionResult(Error.NotFound("The specified activity does not exist."));

        // Same gate as Ingest, but ownership is checked in the service — the owner is only known
        // after loading the row, unlike POST where the body carries the userId.
        Guid? requiredOwnerId = null;
        if (_authOptions.Enabled)
        {
            if (User.Identity?.IsAuthenticated != true)
                return ErrorResults.ToActionResult(Error.Unauthorized("Authentication is required."));

            var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(subject, out var callerId))
                return ErrorResults.ToActionResult(Error.Unauthorized("Authentication is required."));

            requiredOwnerId = callerId;
        }

        var result = await _activities.DeleteAsync(activityId, requiredOwnerId, ct);
        return result.IsSuccess ? NoContent() : ErrorResults.ToActionResult(result.Error!);
    }

    /// <summary>
    /// Serves a user's activities with optional from/to/sport filters, sorting
    /// (<c>sortBy=datetime|points</c>, <c>sortDir=asc|desc</c>) and paging (<c>limit</c>/<c>offset</c>).
    /// Defaults: newest first, no paging. Returns an <c>{ items, totalCount }</c> envelope
    /// carrying the pre-paging total (see README).
    /// </summary>
    [HttpGet("/api/users/{userId}/activities")]
    [ProducesResponseType(typeof(PagedResult<ActivityResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForUser(
        string userId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? sport,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken ct)
    {
        if (!Guid.TryParse(userId, out var id))
            return ErrorResults.ToActionResult(Error.NotFound("The specified user does not exist."));

        // Sort parameters are an allowlist — anything unrecognized is a 400, never passed through.
        ActivitySortField? sortField = sortBy?.Trim().ToLowerInvariant() switch
        {
            null or "" or "datetime" => ActivitySortField.Datetime,
            "points" => ActivitySortField.Points,
            _ => null
        };
        if (sortField is null)
            return ErrorResults.ToActionResult(Error.Validation("'sortBy' must be 'datetime' or 'points'.", "sortBy"));

        bool? ascending = sortDir?.Trim().ToLowerInvariant() switch
        {
            null or "" or "desc" => false,
            "asc" => true,
            _ => null
        };
        if (ascending is null)
            return ErrorResults.ToActionResult(Error.Validation("'sortDir' must be 'asc' or 'desc'.", "sortDir"));

        if (limit is < 1)
            return ErrorResults.ToActionResult(Error.Validation("'limit' must be a positive integer.", "limit"));
        if (offset is < 0)
            return ErrorResults.ToActionResult(Error.Validation("'offset' must be zero or a positive integer.", "offset"));

        var query = new ActivityQuery(from, to, SportQueryParser.Parse(sport), sortField.Value, ascending.Value, limit, offset);
        var result = await _activities.GetForUserAsync(id, query, ct);
        if (!result.IsSuccess)
            return ErrorResults.ToActionResult(result.Error!);

        return Ok(result.Value);
    }
}
