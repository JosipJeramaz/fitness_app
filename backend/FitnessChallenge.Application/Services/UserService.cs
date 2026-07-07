using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Application.Common;
using FitnessChallenge.Application.Dtos.Users;
using FitnessChallenge.Domain.Common;
using FitnessChallenge.Domain.Entities;

namespace FitnessChallenge.Application.Services;

public interface IUserService
{
    Task<Result<RegisterUserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken ct);
    Task<IReadOnlyList<UserListItem>> GetAllAsync(CancellationToken ct);
}

public sealed class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _clock;

    public UserService(IUserRepository users, IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, TimeProvider clock)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    public async Task<Result<RegisterUserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken ct)
    {
        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var lastName = request.LastName?.Trim() ?? string.Empty;

        if (firstName.Length == 0)
            return Error.Validation("'firstName' is required.", "firstName");
        if (lastName.Length == 0)
            return Error.Validation("'lastName' is required.", "lastName");

        var userName = request.UserName?.Trim();
        var hasUserName = !string.IsNullOrEmpty(userName);
        var hasPassword = !string.IsNullOrEmpty(request.Password);

        // Credentials are all-or-nothing: a username without a password (or vice versa) cannot log in.
        if (hasUserName != hasPassword)
            return Error.Validation(
                "'userName' and 'password' must be supplied together to enable login.",
                hasUserName ? "password" : "userName");

        var normalizedFirst = NameNormalizer.Normalize(firstName);
        var normalizedLast = NameNormalizer.Normalize(lastName);

        // Service-layer guard; the DB unique index is the ultimate backstop against races.
        if (await _users.ExistsByNormalizedNameAsync(normalizedFirst, normalizedLast, ct))
            return Error.Conflict("A user with the same first and last name already exists.");

        if (hasUserName && await _users.ExistsByNormalizedUserNameAsync(NameNormalizer.Normalize(userName), ct))
            return Error.Conflict("A user with the same username already exists.");

        var user = User.Register(firstName, lastName, _clock.GetUtcNow());

        if (hasUserName)
            user.SetCredentials(userName!, _passwordHasher.Hash(request.Password!));

        await _users.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<RegisterUserResponse>.Success(new RegisterUserResponse(user.Id.ToString()));
    }

    public async Task<IReadOnlyList<UserListItem>> GetAllAsync(CancellationToken ct)
    {
        var users = await _users.GetAllAsync(ct);
        return users
            .Select(u => new UserListItem(u.Id.ToString(), u.FirstName, u.LastName))
            .ToList();
    }
}
