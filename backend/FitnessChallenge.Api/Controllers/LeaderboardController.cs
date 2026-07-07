using FitnessChallenge.Api.Common;
using FitnessChallenge.Application.Common;
using FitnessChallenge.Application.Dtos.Leaderboard;
using FitnessChallenge.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FitnessChallenge.Api.Controllers;

[ApiController]
[Route("api/leaderboard")]
[Produces("application/json")]
public sealed class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboard;

    public LeaderboardController(ILeaderboardService leaderboard) => _leaderboard = leaderboard;

    /// <summary>
    /// Ranks users by total points and reports a snapshot-free ranking trend. Returns an
    /// <c>{ items, totalCount }</c> envelope (same paging contract as the activities list).
    /// Sortable by <c>rank</c> (default) or <c>trend</c> (ascending hierarchy: up, same, new, down).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<LeaderboardEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] int windowDays = 7,
        [FromQuery] int? limit = null,
        [FromQuery] int? offset = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default)
    {
        // Sort parameters are an allowlist — anything unrecognized is a 400, never passed through.
        LeaderboardSortField? sortField = sortBy?.Trim().ToLowerInvariant() switch
        {
            null or "" or "rank" => LeaderboardSortField.Rank,
            "trend" => LeaderboardSortField.Trend,
            _ => null
        };
        if (sortField is null)
            return ErrorResults.ToActionResult(Error.Validation("'sortBy' must be 'rank' or 'trend'.", "sortBy"));

        bool? ascending = sortDir?.Trim().ToLowerInvariant() switch
        {
            null or "" or "asc" => true,
            "desc" => false,
            _ => null
        };
        if (ascending is null)
            return ErrorResults.ToActionResult(Error.Validation("'sortDir' must be 'asc' or 'desc'.", "sortDir"));

        return Ok(await _leaderboard.GetAsync(windowDays, limit, offset, sortField.Value, ascending.Value, ct));
    }

    /// <summary>
    /// Cumulative points race between the current top users. Lines are anchored at each user's
    /// pre-window total, so the final values match the leaderboard totals. Omit <c>days</c> to start
    /// the chart at the charted users' first activity ("since first activity" mode).
    /// </summary>
    [HttpGet("race")]
    [ProducesResponseType(typeof(RaceChartData), StatusCodes.Status200OK)]
    public async Task<ActionResult<RaceChartData>> GetRace(
        [FromQuery] int? days = null,
        [FromQuery] int top = 5,
        CancellationToken ct = default)
        => Ok(await _leaderboard.GetRaceAsync(days, top, ct));
}
