using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Application.Common;
using FitnessChallenge.Application.Dtos.Auth;
using FitnessChallenge.Domain.Common;
using FitnessChallenge.Domain.Entities;

namespace FitnessChallenge.Application.Services;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
}

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(IUserRepository users, IPasswordHasher passwordHasher, ITokenService tokenService)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.UserName))
            return await LoginWithCredentialsAsync(request, ct);

        if (!string.IsNullOrWhiteSpace(request.FirstName) && !string.IsNullOrWhiteSpace(request.LastName))
            return await LoginByNameAsync(request, ct);

        return Error.Validation("Provide a username and password, or a first and last name.");
    }

    private async Task<Result<LoginResponse>> LoginWithCredentialsAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Password))
            return Error.Validation("'password' is required for username login.", "password");

        var user = await _users.GetByNormalizedUserNameAsync(NameNormalizer.Normalize(request.UserName), ct);

        // Uniform failure for unknown user / no password set / wrong password (avoids user enumeration).
        if (user?.PasswordHash is null || !_passwordHasher.Verify(user.PasswordHash, request.Password))
            return Error.Unauthorized("Invalid credentials.");

        return Success(user);
    }

    /// <summary>
    /// Passwordless fallback for users who never set credentials. A user with a password must use it —
    /// otherwise a public name would bypass their password entirely.
    /// </summary>
    private async Task<Result<LoginResponse>> LoginByNameAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _users.GetByNormalizedNameAsync(
            NameNormalizer.Normalize(request.FirstName), NameNormalizer.Normalize(request.LastName), ct);

        if (user is null)
            return Error.Unauthorized("Invalid credentials.");

        if (user.PasswordHash is not null)
            return Error.Unauthorized("This user has a password — log in with the username and password.");

        return Success(user);
    }

    private Result<LoginResponse> Success(User user)
    {
        var token = _tokenService.CreateToken(user);
        return Result<LoginResponse>.Success(new LoginResponse(token.Token, user.Id.ToString(), token.ExpiresAt));
    }
}
