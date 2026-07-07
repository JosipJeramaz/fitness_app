using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace FitnessChallenge.Tests.Integration;

public class RaceChartTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public RaceChartTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Race_series_end_at_the_leaderboard_totals()
    {
        // Two users with activities inside and before the 30-day window (real clock -> relative dates).
        var now = DateTimeOffset.UtcNow;
        var fast = await RegisterAsync("Race", "Leader");
        var slow = await RegisterAsync("Race", "Chaser");

        await IngestAsync(fast, now.AddDays(-40), "running", 10.0);  // pre-window -> baseline (1000)
        await IngestAsync(fast, now.AddDays(-5), "running", 5.0);   // in window   (+500)
        await IngestAsync(slow, now.AddDays(-3), "cycling", 20.0);  // in window   (+500)

        var race = await _client.GetFromJsonAsync<JsonElement>("/api/leaderboard/race?days=30&top=5");
        var leaderboard = await _client.GetFromJsonAsync<JsonElement>("/api/leaderboard");

        race.GetProperty("dates").GetArrayLength().Should().Be(31); // 30-day window -> 31 daily buckets

        foreach (var series in race.GetProperty("series").EnumerateArray())
        {
            var userId = series.GetProperty("userId").GetString();
            var points = series.GetProperty("cumulativePoints").EnumerateArray()
                .Select(p => p.ValueKind == JsonValueKind.Null ? (long?)null : p.GetInt64())
                .ToList();

            var total = leaderboard.GetProperty("items").EnumerateArray()
                .Single(e => e.GetProperty("userId").GetString() == userId)
                .GetProperty("totalPoints").GetInt64();

            points[^1].Should().Be(total, "the last point of every line must equal the leaderboard total");

            // Nulls (no line yet) may only appear before the athlete's first activity — never after it.
            var firstValue = points.FindIndex(p => p.HasValue);
            firstValue.Should().BeGreaterThanOrEqualTo(0);
            points.Skip(firstValue).Should().OnlyContain(p => p.HasValue, "once a line starts it never disappears");
            points.Where(p => p.HasValue).Select(p => p!.Value)
                .Should().BeInAscendingOrder("cumulative points can never decrease");
        }
    }

    [Fact]
    public async Task Since_first_activity_mode_starts_at_the_earliest_charted_activity()
    {
        var now = DateTimeOffset.UtcNow;
        var user = await RegisterAsync("Race", "Pioneer");

        // Oldest activity in this test class's database: 50 days back.
        await IngestAsync(user, now.AddDays(-50), "running", 3.0);
        await IngestAsync(user, now.AddDays(-1), "running", 2.0);

        var race = await _client.GetFromJsonAsync<JsonElement>("/api/leaderboard/race?top=20"); // days omitted

        var dates = race.GetProperty("dates").EnumerateArray().Select(d => d.GetString()!).ToList();
        dates[0].Should().Be(now.AddDays(-50).UtcDateTime.Date.AddDays(-1).ToString("yyyy-MM-dd"),
            "the chart starts exactly one lead-in day before the earliest charted activity — no weeks of empty lead-in");
        dates[^1].Should().Be(now.UtcDateTime.Date.ToString("yyyy-MM-dd"));

        var pioneer = race.GetProperty("series").EnumerateArray()
            .Single(s => s.GetProperty("userId").GetString() == user);
        var points = pioneer.GetProperty("cumulativePoints").EnumerateArray()
            .Select(p => p.ValueKind == JsonValueKind.Null ? (long?)null : p.GetInt64())
            .ToList();
        points[0].Should().BeNull("the lead-in day has no line yet");
        points[1].Should().Be(300, "the line begins mid-air on the first activity day");
        points[^1].Should().Be(500); // 300 + 200
    }

    private async Task<string> RegisterAsync(string first, string last)
    {
        var response = await _client.PostAsJsonAsync("/api/users",
            new { firstName = first, lastName = last + Guid.NewGuid().ToString("N")[..8] });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task IngestAsync(string userId, DateTimeOffset when, string sport, double distance)
    {
        var response = await _client.PostAsJsonAsync("/api/activities",
            new { userId, datetime = when.ToString("O"), sport, distance });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
