using FitnessChallenge.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitnessChallenge.Api.Common;

/// <summary>Maps an application-layer <see cref="Error"/> to a consistent RFC-9457 problem response.</summary>
public static class ErrorResults
{
    public static ObjectResult ToActionResult(Error error) => error.Type switch
    {
        ErrorType.Validation => ValidationProblem(error),
        ErrorType.Unauthorized => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", error.Message),
        ErrorType.Forbidden => Problem(StatusCodes.Status403Forbidden, "Forbidden", error.Message),
        ErrorType.NotFound => Problem(StatusCodes.Status404NotFound, "Not Found", error.Message),
        ErrorType.Conflict => Problem(StatusCodes.Status409Conflict, "Conflict", error.Message),
        _ => Problem(StatusCodes.Status500InternalServerError, "Error", error.Message)
    };

    private static ObjectResult ValidationProblem(Error error)
    {
        var errors = new Dictionary<string, string[]>
        {
            [error.Field ?? "request"] = new[] { error.Message }
        };

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest
        };

        return new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
    }

    private static ObjectResult Problem(int status, string title, string detail)
        => new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}
