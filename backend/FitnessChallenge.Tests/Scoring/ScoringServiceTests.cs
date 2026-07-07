using System.Globalization;
using FitnessChallenge.Domain.Enums;
using FitnessChallenge.Domain.Scoring;
using FluentAssertions;
using Xunit;

namespace FitnessChallenge.Tests.Scoring;

/// <summary>
/// The most important suite: every worked example from the assignment's scoring rules plus boundary cases.
/// Distance values are passed as strings and parsed as <see cref="decimal"/> to exercise the
/// exact decimal path the ingestion uses (guards against binary floating-point drift).
/// </summary>
public class ScoringServiceTests
{
    private readonly IScoringService _sut = new ScoringService();

    [Theory]
    // ---- assignment worked examples ----
    [InlineData(ActivityType.Running, "42.195", 4219)] // 4219.5  -> 4219
    [InlineData(ActivityType.Walking, "1.55", 77)]     // 77.5    -> 77
    [InlineData(ActivityType.Walking, "2.0", 100)]     // exact
    [InlineData(ActivityType.Cycling, "10", 250)]      // exact
    // ---- boundary / decimal-safety ----
    [InlineData(ActivityType.Running, "0.29", 29)]     // must be 29, NOT 28 (double would drift)
    [InlineData(ActivityType.Cycling, "0.04", 1)]      // 1.0 exact
    [InlineData(ActivityType.Walking, "0.01", 0)]      // 0.5 -> 0
    [InlineData(ActivityType.Running, "0.009", 0)]     // 0.9 -> 0
    public void Distance_sports_multiply_then_floor(ActivityType type, string distanceKm, int expected)
    {
        var km = decimal.Parse(distanceKm, CultureInfo.InvariantCulture);

        _sut.CalculatePoints(type, km, null, null).Should().Be(expected);
    }

    [Theory]
    // ---- assignment worked examples ---- (mm:ss shown in comments, passed as total seconds)
    [InlineData(ActivityType.Swimming, 1845, 450)] // 30:45 -> 30 min
    [InlineData(ActivityType.Swimming, 115, 15)]   // 1:55  -> 1 min
    [InlineData(ActivityType.Gym, 2720, 225)]      // 45:20 -> 45 min
    [InlineData(ActivityType.Gym, 59, 0)]          // 0:59  -> 0 min
    // ---- boundary ----
    [InlineData(ActivityType.Swimming, 120, 30)]   // 2:00  -> 2 min
    [InlineData(ActivityType.Gym, 60, 5)]          // 1:00  -> 1 min
    [InlineData(ActivityType.Swimming, 119, 15)]   // 1:59  -> 1 min
    public void Duration_sports_floor_to_whole_minutes_then_multiply(ActivityType type, int durationSeconds, int expected)
    {
        _sut.CalculatePoints(type, null, durationSeconds, null).Should().Be(expected);
    }

    [Theory]
    // ---- assignment worked examples ----
    [InlineData(399, 3)]     // -> 300 -> 3
    [InlineData(10000, 100)]
    [InlineData(99, 0)]
    // ---- boundary ----
    [InlineData(100, 1)]
    [InlineData(250, 2)]
    [InlineData(1, 0)]
    public void Daily_steps_floor_to_hundreds(int steps, int expected)
    {
        _sut.CalculatePoints(ActivityType.DailySteps, null, null, steps).Should().Be(expected);
    }

    [Fact]
    public void Missing_required_metric_throws()
    {
        var act = () => _sut.CalculatePoints(ActivityType.Running, null, null, null);

        act.Should().Throw<ArgumentNullException>();
    }
}
