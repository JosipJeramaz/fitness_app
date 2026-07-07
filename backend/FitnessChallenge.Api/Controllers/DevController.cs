using FitnessChallenge.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc;

namespace FitnessChallenge.Api.Controllers;

[ApiController]
[Route("api/dev")]
[Produces("application/json")]
public sealed class DevController : ControllerBase
{
    private readonly DataSeeder _seeder;
    private readonly IHostEnvironment _environment;

    public DevController(DataSeeder seeder, IHostEnvironment environment)
    {
        _seeder = seeder;
        _environment = environment;
    }

    /// <summary>Seeds demo users + backdated activities. Development only; idempotent.</summary>
    [HttpPost("seed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Seed(CancellationToken ct)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        var inserted = await _seeder.SeedAsync(ct);
        return Ok(new { seeded = inserted > 0, rowsWritten = inserted });
    }
}
