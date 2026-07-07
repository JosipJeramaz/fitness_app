using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace FitnessChallenge.Tests.Integration;

/// <summary>
/// End-to-end proof of the serve-activities sorting + paging contract. One user, three activities
/// with distinct points and datetimes:
///   running 10 km   @ 06-10  -> 1000 pts
///   walking 1 km    @ 06-20  ->   50 pts
///   cycling 20 km   @ 06-30  ->  500 pts
/// </summary>
public class ActivitySortingPagingTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ActivitySortingPagingTests(ApiFactory factory) => _client = factory.CreateClient();

    private async Task<string> SeedUserWithActivitiesAsync()
    {
        var register = await _client.PostAsJsonAsync("/api/users",
            new { firstName = "Sort", lastName = "Paging" + Guid.NewGuid().ToString("N")[..8] });
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var seeds = new (string sport, double distance, string datetime)[]
        {
            ("running", 10.0, "2026-06-10T08:00:00Z"),
            ("walking", 1.0, "2026-06-20T08:00:00Z"),
            ("cycling", 20.0, "2026-06-30T08:00:00Z"),
        };
        foreach (var (sport, distance, datetime) in seeds)
        {
            var response = await _client.PostAsJsonAsync("/api/activities", new { userId, datetime, sport, distance });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        return userId;
    }

    private async Task<(List<(string Type, int Points)> Items, int TotalCount)> GetPageAsync(string userId, string queryString)
    {
        var response = await _client.GetAsync($"/api/users/{userId}/activities{queryString}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray()
            .Select(a => (a.GetProperty("activityType").GetString()!, a.GetProperty("points").GetInt32()))
            .ToList();
        return (items, body.GetProperty("totalCount").GetInt32());
    }

    private async Task<List<(string Type, int Points)>> GetAsync(string userId, string queryString)
        => (await GetPageAsync(userId, queryString)).Items;

    [Fact]
    public async Task Default_is_newest_first()
    {
        var userId = await SeedUserWithActivitiesAsync();

        var rows = await GetAsync(userId, "");

        rows.Select(r => r.Type).Should().ContainInOrder("cycling", "walking", "running");
    }

    [Fact]
    public async Task Sorts_by_points_in_both_directions()
    {
        var userId = await SeedUserWithActivitiesAsync();

        (await GetAsync(userId, "?sortBy=points&sortDir=desc"))
            .Select(r => r.Points).Should().ContainInOrder(1000, 500, 50);

        (await GetAsync(userId, "?sortBy=points&sortDir=asc"))
            .Select(r => r.Points).Should().ContainInOrder(50, 500, 1000);
    }

    [Fact]
    public async Task Pages_are_disjoint_and_ordered_and_report_the_full_total()
    {
        var userId = await SeedUserWithActivitiesAsync();

        var page1 = await GetPageAsync(userId, "?sortBy=points&sortDir=desc&limit=2&offset=0");
        var page2 = await GetPageAsync(userId, "?sortBy=points&sortDir=desc&limit=2&offset=2");

        page1.Items.Select(r => r.Points).Should().ContainInOrder(1000, 500);
        page2.Items.Select(r => r.Points).Should().ContainInOrder(50);
        page1.Items.Concat(page2.Items).Should().OnlyHaveUniqueItems(); // no overlap, nothing lost

        // totalCount is the pre-paging total on every page, so "X of N" stays stable while paging.
        page1.TotalCount.Should().Be(3);
        page2.TotalCount.Should().Be(3);
    }

    [Theory]
    [InlineData("?sortBy=hack")]
    [InlineData("?sortDir=sideways")]
    [InlineData("?limit=0")]
    [InlineData("?limit=-5")]
    [InlineData("?offset=-1")]
    public async Task Invalid_sort_or_paging_parameters_return_400(string queryString)
    {
        var userId = await SeedUserWithActivitiesAsync();

        var response = await _client.GetAsync($"/api/users/{userId}/activities{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
