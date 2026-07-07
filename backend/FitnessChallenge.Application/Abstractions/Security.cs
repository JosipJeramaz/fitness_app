using FitnessChallenge.Domain.Entities;

namespace FitnessChallenge.Application.Abstractions;

/// <summary>Password hashing port (implemented over ASP.NET Core's PasswordHasher in Infrastructure).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>JWT issuing port (implemented in Infrastructure).</summary>
public interface ITokenService
{
    AccessToken CreateToken(User user);
}
