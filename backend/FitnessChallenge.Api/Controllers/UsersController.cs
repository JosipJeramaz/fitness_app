using FitnessChallenge.Api.Common;
using FitnessChallenge.Application.Dtos.Users;
using FitnessChallenge.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FitnessChallenge.Api.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    /// <summary>Registers a user. Rejects a duplicate (first, last) name pair with 409.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request, CancellationToken ct)
    {
        var result = await _users.RegisterAsync(request, ct);
        if (!result.IsSuccess)
            return ErrorResults.ToActionResult(result.Error!);

        return CreatedAtAction(nameof(GetAll), new { }, result.Value);
    }

    /// <summary>Lists registered users (drives the frontend user selector).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserListItem>>> GetAll(CancellationToken ct)
        => Ok(await _users.GetAllAsync(ct));
}
