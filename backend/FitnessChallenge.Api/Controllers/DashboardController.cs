using FitnessChallenge.Api.Common;
using FitnessChallenge.Application.Common;
using FitnessChallenge.Application.Dtos.Dashboard;
using FitnessChallenge.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FitnessChallenge.Api.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    /// <summary>Returns server-aggregated dashboard data (totals, points-over-time, sport breakdown).</summary>
    [HttpGet("/api/users/{userId}/dashboard")]
    [ProducesResponseType(typeof(DashboardData), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string userId, [FromQuery] string groupBy = "day", CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var id))
            return ErrorResults.ToActionResult(Error.NotFound("The specified user does not exist."));

        var result = await _dashboard.GetAsync(id, groupBy, ct);
        if (!result.IsSuccess)
            return ErrorResults.ToActionResult(result.Error!);

        return Ok(result.Value);
    }
}
