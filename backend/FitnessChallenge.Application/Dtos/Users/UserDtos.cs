namespace FitnessChallenge.Application.Dtos.Users;

/// <summary>
/// Registration payload. <see cref="UserName"/> and <see cref="Password"/> are optional but must be
/// supplied together — they enable the JWT login feature.
/// </summary>
public sealed record RegisterUserRequest
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? UserName { get; init; }
    public string? Password { get; init; }
}

/// <summary>Registration result — just the new id, per the API contract.</summary>
public sealed record RegisterUserResponse(string Id);

/// <summary>Lightweight user row for the frontend user selector.</summary>
public sealed record UserListItem(string Id, string FirstName, string LastName);
