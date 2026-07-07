using FitnessChallenge.Application.Dtos.Users;
using FluentValidation;

namespace FitnessChallenge.Application.Users;

/// <summary>
/// Ensures both names are present and non-empty after trimming. Username and password are optional
/// but enable login, so one without the other is rejected; the username format is also checked here.
/// </summary>
public sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("'firstName' is required.")
            .OverridePropertyName("firstName");

        RuleFor(x => x.LastName)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("'lastName' is required.")
            .OverridePropertyName("lastName");

        RuleFor(x => x.UserName)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .When(x => !string.IsNullOrEmpty(x.Password))
            .WithMessage("'userName' is required when a password is supplied.")
            .OverridePropertyName("userName");

        RuleFor(x => x.Password)
            .Must(pw => !string.IsNullOrEmpty(pw))
            .When(x => !string.IsNullOrWhiteSpace(x.UserName))
            .WithMessage("'password' is required when a username is supplied.")
            .OverridePropertyName("password");

        RuleFor(x => x.UserName)
            .Matches("^[a-zA-Z0-9._-]{3,50}$")
            .When(x => !string.IsNullOrWhiteSpace(x.UserName))
            .WithMessage("'userName' must be 3–50 characters using letters, digits, '.', '_' or '-'.")
            .OverridePropertyName("userName");
    }
}
