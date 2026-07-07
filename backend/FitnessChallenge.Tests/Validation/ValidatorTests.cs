using FitnessChallenge.Application.Activities;
using FitnessChallenge.Application.Dtos.Activities;
using FitnessChallenge.Application.Dtos.Users;
using FitnessChallenge.Application.Users;
using FitnessChallenge.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace FitnessChallenge.Tests.Validation;

/// <summary>Covers the FluentValidation wrappers (userId + name presence), not the matrix itself.</summary>
public class ValidatorTests
{
    private const string ValidDatetime = "2026-06-30T10:30:00Z";
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);
    private readonly IngestActivityRequestValidator _activityValidator = new(new FixedTimeProvider(FixedNow));
    private readonly RegisterUserRequestValidator _userValidator = new();

    [Fact]
    public void Fully_valid_activity_passes()
    {
        var request = new IngestActivityRequest
        {
            UserId = "11111111-1111-1111-1111-111111111111",
            Datetime = ValidDatetime,
            Sport = "running",
            Distance = 42.195m
        };

        _activityValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void Missing_or_malformed_userId_fails(string? userId)
    {
        var request = new IngestActivityRequest
        {
            UserId = userId,
            Datetime = ValidDatetime,
            Sport = "running",
            Distance = 5m
        };

        var result = _activityValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "userId");
    }

    [Fact]
    public void Swimming_with_distance_fails_via_validator()
    {
        var request = new IngestActivityRequest
        {
            UserId = "11111111-1111-1111-1111-111111111111",
            Datetime = ValidDatetime,
            Sport = "swimming",
            Distance = 42.195m
        };

        var result = _activityValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "distance");
    }

    [Theory]
    [InlineData(null, "Doe")]
    [InlineData("John", null)]
    [InlineData("   ", "Doe")]
    [InlineData("John", "  ")]
    public void Registration_requires_both_names(string? firstName, string? lastName)
    {
        var result = _userValidator.Validate(new RegisterUserRequest { FirstName = firstName, LastName = lastName });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Registration_with_both_names_passes()
    {
        var result = _userValidator.Validate(new RegisterUserRequest { FirstName = "John", LastName = "Doe" });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Registration_with_full_credentials_passes()
    {
        var result = _userValidator.Validate(new RegisterUserRequest
        {
            FirstName = "John", LastName = "Doe", UserName = "john.doe", Password = "secret-pw"
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Registration_with_password_but_no_username_fails()
    {
        var result = _userValidator.Validate(new RegisterUserRequest
        {
            FirstName = "John", LastName = "Doe", Password = "secret-pw"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "userName");
    }

    [Fact]
    public void Registration_with_username_but_no_password_fails()
    {
        var result = _userValidator.Validate(new RegisterUserRequest
        {
            FirstName = "John", LastName = "Doe", UserName = "john.doe"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "password");
    }

    [Theory]
    [InlineData("ab")]            // too short
    [InlineData("john doe")]      // whitespace
    [InlineData("john@doe")]      // disallowed character
    public void Registration_with_malformed_username_fails(string userName)
    {
        var result = _userValidator.Validate(new RegisterUserRequest
        {
            FirstName = "John", LastName = "Doe", UserName = userName, Password = "secret-pw"
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "userName");
    }
}
