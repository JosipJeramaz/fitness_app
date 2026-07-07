namespace FitnessChallenge.Application.Dtos.Auth;

/// <summary>
/// Login request with two modes: username + password (credential login), or first + last name only —
/// a convenience fallback allowed solely for users who never set credentials.
/// </summary>
public sealed record LoginRequest
{
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}

/// <summary>Issued bearer token plus the owning user id and expiry.</summary>
public sealed record LoginResponse(string Token, string UserId, DateTimeOffset ExpiresAt);
