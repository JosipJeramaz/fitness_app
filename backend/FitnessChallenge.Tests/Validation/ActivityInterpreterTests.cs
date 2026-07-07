using System.Globalization;
using FitnessChallenge.Application.Activities;
using FitnessChallenge.Application.Dtos.Activities;
using FitnessChallenge.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FitnessChallenge.Tests.Validation;

/// <summary>
/// Table-driven proof of the ingestion validation matrix against the single source of truth
/// (<see cref="ActivityInterpreter"/>). Every legal combination resolves; every illegal one fails on the
/// expected field.
/// </summary>
public class ActivityInterpreterTests
{
    private const string ValidDatetime = "2026-06-30T10:30:00Z";
    private const string ValidUserId = "11111111-1111-1111-1111-111111111111";
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("running", "5", null, null, ActivityType.Running)]
    [InlineData("walking", "1.55", null, null, ActivityType.Walking)]
    [InlineData("cycling", "10", null, null, ActivityType.Cycling)]
    [InlineData("gym", null, "45:20", null, ActivityType.Gym)]
    [InlineData("swimming", null, "30:45", null, ActivityType.Swimming)]
    [InlineData(null, null, null, 399, ActivityType.DailySteps)]
    [InlineData("RUNNING", "5", null, null, ActivityType.Running)] // sport is case-insensitive
    // at the per-entry plausibility caps — must still pass
    [InlineData("running", "100", null, null, ActivityType.Running)]
    [InlineData("walking", "80", null, null, ActivityType.Walking)]
    [InlineData("cycling", "300", null, null, ActivityType.Cycling)]
    [InlineData("gym", null, "1080:00", null, ActivityType.Gym)]       // exactly 18 h
    [InlineData("swimming", null, "1080:00", null, ActivityType.Swimming)]
    [InlineData(null, null, null, 100000, ActivityType.DailySteps)]
    // at the "worth exactly 1 point" floor — must still pass
    [InlineData("running", "0.01", null, null, ActivityType.Running)]   // 0.01 * 100 = 1 pt
    [InlineData("walking", "0.02", null, null, ActivityType.Walking)]   // 0.02 * 50  = 1 pt
    [InlineData("cycling", "0.04", null, null, ActivityType.Cycling)]   // 0.04 * 25  = 1 pt
    [InlineData("gym", null, "1:00", null, ActivityType.Gym)]           // 1 min -> 5 pts
    [InlineData("swimming", null, "1:00", null, ActivityType.Swimming)] // 1 min -> 15 pts
    [InlineData(null, null, null, 100, ActivityType.DailySteps)]        // 100 / 100 = 1 pt
    public void Valid_combinations_resolve_to_expected_type(string? sport, string? distance, string? duration, int? steps, ActivityType expectedType)
    {
        var result = ActivityInterpreter.Interpret(Build(sport, distance, duration, steps), FixedNow);

        result.IsValid.Should().BeTrue();
        result.Type.Should().Be(expectedType);
    }

    [Fact]
    public void Datetime_exactly_one_year_back_is_still_valid()
    {
        var request = Build("running", "5", null, null, datetime: "2025-06-30T12:00:00Z"); // == FixedNow - 1 year

        var result = ActivityInterpreter.Interpret(request, FixedNow);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Datetime_slightly_ahead_of_the_clock_is_tolerated()
    {
        // Client clocks drift — a few minutes ahead of the server must still count as "now".
        var request = Build("running", "5", null, null, datetime: "2026-06-30T12:04:00Z"); // FixedNow + 4 min

        var result = ActivityInterpreter.Interpret(request, FixedNow);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    // wrong metric for the sport
    [InlineData("swimming", "42.195", null, null, ValidDatetime, "distance")] // spec's canonical invalid example
    [InlineData("running", null, "10:00", null, ValidDatetime, "duration")]
    [InlineData("gym", "5", null, null, ValidDatetime, "distance")]
    // steps sent together with a sport
    [InlineData("running", null, null, 100, ValidDatetime, "steps")]
    // missing required metric
    [InlineData("running", null, null, null, ValidDatetime, "distance")]
    [InlineData("swimming", null, null, null, ValidDatetime, "duration")]
    // multiple / conflicting metrics
    [InlineData("running", "5", "1:00", null, ValidDatetime, "duration")]
    [InlineData(null, "5", null, 100, ValidDatetime, "distance")] // daily steps must not carry distance
    // bad metric values
    [InlineData("gym", null, "1:60", null, ValidDatetime, "duration")] // seconds out of range
    [InlineData("gym", null, "abc", null, ValidDatetime, "duration")]  // wrong format
    [InlineData("gym", null, "0:00", null, ValidDatetime, "duration")] // resolves to 0 seconds
    [InlineData("running", "0", null, null, ValidDatetime, "distance")] // distance must be > 0
    [InlineData(null, null, null, 0, ValidDatetime, "steps")]           // steps must be positive
    [InlineData(null, null, null, null, ValidDatetime, "steps")]        // no sport, no steps
    // below the "worth at least 1 point" floor (would score 0)
    [InlineData("running", "0.009", null, null, ValidDatetime, "distance")]  // 0.9 pts -> 0
    [InlineData("walking", "0.019", null, null, ValidDatetime, "distance")]  // 0.95 pts -> 0
    [InlineData("cycling", "0.039", null, null, ValidDatetime, "distance")]  // 0.975 pts -> 0
    [InlineData("gym", null, "0:59", null, ValidDatetime, "duration")]       // 0 completed minutes -> 0
    [InlineData("swimming", null, "0:59", null, ValidDatetime, "duration")]
    [InlineData(null, null, null, 99, ValidDatetime, "steps")]               // < 100 -> 0
    // just above the per-entry plausibility caps
    [InlineData("running", "100.001", null, null, ValidDatetime, "distance")]
    [InlineData("walking", "80.001", null, null, ValidDatetime, "distance")]
    [InlineData("cycling", "300.001", null, null, ValidDatetime, "distance")]
    [InlineData("gym", null, "1080:01", null, ValidDatetime, "duration")]      // one second over 18 h
    [InlineData("swimming", null, "1080:01", null, ValidDatetime, "duration")]
    [InlineData(null, null, null, 100001, ValidDatetime, "steps")]
    // unknown sport
    [InlineData("jumping", "5", null, null, ValidDatetime, "sport")]
    // datetime problems
    [InlineData("running", "5", null, null, "not-a-date", "datetime")]
    [InlineData("running", "5", null, null, null, "datetime")]
    [InlineData("running", "5", null, null, "2025-06-30T11:59:59Z", "datetime")] // one second beyond a year back
    [InlineData("running", "5", null, null, "2026-06-30T12:05:01Z", "datetime")] // future, past the skew tolerance
    public void Invalid_combinations_fail_on_expected_field(string? sport, string? distance, string? duration, int? steps, string? datetime, string expectedField)
    {
        var result = ActivityInterpreter.Interpret(Build(sport, distance, duration, steps, datetime), FixedNow);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == expectedField);
    }

    private static IngestActivityRequest Build(string? sport, string? distance, string? duration, int? steps, string? datetime = ValidDatetime)
        => new()
        {
            UserId = ValidUserId,
            Datetime = datetime,
            Sport = sport,
            Distance = distance is null ? null : decimal.Parse(distance, CultureInfo.InvariantCulture),
            Duration = duration,
            Steps = steps
        };
}
