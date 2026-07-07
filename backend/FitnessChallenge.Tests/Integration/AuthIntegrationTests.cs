using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace FitnessChallenge.Tests.Integration;

public class AuthIntegrationTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public AuthIntegrationTests(AuthApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_returns_token_and_enforces_activity_ownership()
    {
        var client = _factory.CreateClient();

        var aliceId = await RegisterAsync(client, "Alice", "Anderson", "alice.anderson", "secret-pw-1");
        var bobId = await RegisterAsync(client, "Bob", "Baker", "bob.baker", "secret-pw-2");

        // Anonymous ingest is rejected when auth is enabled.
        var anonymous = await client.PostAsJsonAsync("/api/activities",
            new { userId = aliceId, datetime = "2026-06-30T10:30:00Z", sport = "running", distance = 5.0 });
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var aliceToken = await LoginAsync(client, "alice.anderson", "secret-pw-1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aliceToken);

        // Alice can log her own activity.
        var own = await client.PostAsJsonAsync("/api/activities",
            new { userId = aliceId, datetime = "2026-06-30T10:30:00Z", sport = "running", distance = 5.0 });
        own.StatusCode.Should().Be(HttpStatusCode.Created);

        // Alice cannot log an activity for Bob.
        var forbidden = await client.PostAsJsonAsync("/api/activities",
            new { userId = bobId, datetime = "2026-06-30T10:30:00Z", sport = "running", distance = 5.0 });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_enforces_activity_ownership()
    {
        var client = _factory.CreateClient();

        var aliceId = await RegisterAsync(client, "Iva", "Ivic", "iva.ivic", "secret-pw-1");
        await RegisterAsync(client, "Jan", "Novak", "jan.novak", "secret-pw-2");

        // Alice logs an activity to delete.
        var aliceToken = await LoginAsync(client, "iva.ivic", "secret-pw-1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aliceToken);
        var created = await client.PostAsJsonAsync("/api/activities",
            new { userId = aliceId, datetime = "2026-06-30T10:30:00Z", sport = "running", distance = 5.0 });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var activityId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        // Anonymous delete is rejected.
        client.DefaultRequestHeaders.Authorization = null;
        var anonymous = await client.DeleteAsync($"/api/activities/{activityId}");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Another user cannot delete Alice's activity.
        var bobToken = await LoginAsync(client, "jan.novak", "secret-pw-2");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bobToken);
        var forbidden = await client.DeleteAsync($"/api/activities/{activityId}");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The owner can — and a repeat delete is a 404 (the row is gone).
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aliceToken);
        var deleted = await client.DeleteAsync($"/api/activities/{activityId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var repeat = await client.DeleteAsync($"/api/activities/{activityId}");
        repeat.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The activity no longer shows up in her list.
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/users/{aliceId}/activities");
        list.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, "Carol", "Clark", "carol.clark", "correct-pw");

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = "carol.clark", password = "wrong-pw" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_is_case_insensitive_on_username()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, "Dave", "Dixon", "dave.dixon", "secret-pw");

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = "DAVE.Dixon", password = "secret-pw" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_with_duplicate_username_conflicts()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, "Erin", "Evans", "erin", "secret-pw");

        var response = await client.PostAsJsonAsync("/api/users",
            new { firstName = "Erik", lastName = "Evans", userName = "ERIN", password = "other-pw" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_with_password_but_no_username_is_rejected()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users",
            new { firstName = "Frank", lastName = "Foster", password = "secret-pw" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Name_login_works_for_credentialless_user_and_scopes_ingest_to_self()
    {
        var client = _factory.CreateClient();

        // Registered without username/password — the name fallback is their only way in.
        var register = await client.PostAsJsonAsync("/api/users", new { firstName = "Greta", lastName = "Garbo" });
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var gretaId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var login = await client.PostAsJsonAsync("/api/auth/login", new { firstName = "Greta", lastName = "Garbo" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var own = await client.PostAsJsonAsync("/api/activities",
            new { userId = gretaId, datetime = "2026-06-30T10:30:00Z", sport = "running", distance = 5.0 });
        own.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Name_login_is_rejected_when_the_user_has_credentials()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, "Hana", "Horvat", "hana.horvat", "secret-pw");

        // A public name must never bypass a set password.
        var response = await client.PostAsJsonAsync("/api/auth/login", new { firstName = "Hana", lastName = "Horvat" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Name_login_with_unknown_name_is_unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { firstName = "No", lastName = "Body" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string first, string last, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/users",
            new { firstName = first, lastName = last, userName, password });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task<string> LoginAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }
}
