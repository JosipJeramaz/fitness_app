using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace FitnessChallenge.Tests.Integration;

public class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Register_ingest_serve_leaderboard_dashboard_happy_path()
    {
        // Register
        var register = await _client.PostAsJsonAsync("/api/users", new { firstName = "Ada", lastName = "Lovelace" });
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        // Duplicate (case-insensitive + trimmed) -> 409
        var duplicate = await _client.PostAsJsonAsync("/api/users", new { firstName = "  ada ", lastName = "LOVELACE" });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Valid running ingestion -> 201 with 4219 points
        var ingest = await _client.PostAsJsonAsync("/api/activities",
            new { userId, datetime = "2026-06-30T10:30:00Z", sport = "running", distance = 42.195 });
        ingest.StatusCode.Should().Be(HttpStatusCode.Created);
        var ingestBody = await ingest.Content.ReadFromJsonAsync<JsonElement>();
        ingestBody.GetProperty("points").GetInt32().Should().Be(4219);
        ingestBody.GetProperty("activityType").GetString().Should().Be("running");

        // Invalid swimming + distance -> 400
        var invalid = await _client.PostAsJsonAsync("/api/activities",
            new { userId, datetime = "2026-06-30T10:30:00Z", sport = "swimming", distance = 42.195 });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Daily steps (no sport) -> 201
        var steps = await _client.PostAsJsonAsync("/api/activities",
            new { userId, datetime = "2026-06-29T08:00:00Z", steps = 399 });
        steps.StatusCode.Should().Be(HttpStatusCode.Created);

        // Serve — { items, totalCount } envelope (see README)
        var serve = await _client.GetFromJsonAsync<JsonElement>($"/api/users/{userId}/activities");
        serve.GetProperty("items").GetArrayLength().Should().Be(2);
        serve.GetProperty("totalCount").GetInt32().Should().Be(2);

        // Leaderboard — { items, totalCount } envelope — contains the user with the summed points
        var leaderboard = await _client.GetFromJsonAsync<JsonElement>("/api/leaderboard");
        leaderboard.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        var entry = leaderboard.GetProperty("items").EnumerateArray()
            .Single(e => e.GetProperty("userId").GetString() == userId);
        entry.GetProperty("totalPoints").GetInt64().Should().Be(4222); // 4219 + 3

        // Dashboard
        var dashboard = await _client.GetFromJsonAsync<JsonElement>($"/api/users/{userId}/dashboard");
        dashboard.GetProperty("totalPoints").GetInt64().Should().Be(4222);
        dashboard.GetProperty("activityCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Ingesting_for_unknown_user_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/activities", new
        {
            userId = Guid.NewGuid().ToString(),
            datetime = "2026-06-30T10:30:00Z",
            sport = "running",
            distance = 5.0
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Registration_requires_names()
    {
        var response = await _client.PostAsJsonAsync("/api/users", new { firstName = "", lastName = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
