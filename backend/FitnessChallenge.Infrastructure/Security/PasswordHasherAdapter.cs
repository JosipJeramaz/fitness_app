using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace FitnessChallenge.Infrastructure.Security;

/// <summary>Wraps ASP.NET Core's battle-tested PBKDF2 <see cref="PasswordHasher{TUser}"/>.</summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private static readonly User Dummy = User.Register("hash", "context", DateTimeOffset.UnixEpoch);
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(Dummy, password);

    public bool Verify(string hash, string password)
        => _inner.VerifyHashedPassword(Dummy, hash, password) != PasswordVerificationResult.Failed;
}
