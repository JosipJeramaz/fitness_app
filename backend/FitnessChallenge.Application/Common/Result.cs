namespace FitnessChallenge.Application.Common;

/// <summary>Category of a business failure, mapped to an HTTP status by the API layer.</summary>
public enum ErrorType
{
    Validation,   // 400
    Unauthorized, // 401
    Forbidden,    // 403
    NotFound,     // 404
    Conflict      // 409
}

/// <summary>A business failure with an optional field name (used to key ValidationProblemDetails).</summary>
public sealed record Error(ErrorType Type, string Message, string? Field = null)
{
    public static Error Validation(string message, string? field = null) => new(ErrorType.Validation, message, field);
    public static Error Unauthorized(string message) => new(ErrorType.Unauthorized, message);
    public static Error Forbidden(string message) => new(ErrorType.Forbidden, message);
    public static Error NotFound(string message, string? field = null) => new(ErrorType.NotFound, message, field);
    public static Error Conflict(string message, string? field = null) => new(ErrorType.Conflict, message, field);
}

/// <summary>
/// Lightweight result carrying either a value or an <see cref="Common.Error"/>. Keeps business-flow
/// outcomes (not-found, conflict, validation) out of the exception path so controllers map them explicitly.
/// </summary>
public readonly struct Result<T>
{
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default, error);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
