using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Domain.Entities;
using FitnessChallenge.Domain.Scoring;
using FitnessChallenge.Infrastructure.Data;
using FitnessChallenge.Infrastructure.Security;
using FitnessChallenge.Infrastructure.Seeding;
using FitnessChallenge.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FitnessChallenge.Tests.Seeding;

public class DataSeederTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Seeds_five_users_whose_oldest_activity_is_exactly_40_days_old()
    {
        await using var db = CreateContext();
        var seeder = new DataSeeder(db, new ScoringService(), new PasswordHasherAdapter(), new FixedTimeProvider(Now));

        var inserted = await seeder.SeedAsync();

        inserted.Should().BeGreaterThan(0);
        var users = await db.Users.ToListAsync();
        users.Should().HaveCount(5);

        foreach (var user in users)
        {
            var oldest = await db.Activities
                .Where(a => a.UserId == user.Id)
                .OrderBy(a => a.DateTimeUtc)
                .FirstAsync();

            // The hour-of-day is randomized for realism even on the oldest entry, so compare calendar days.
            oldest.DateTimeUtc.UtcDateTime.Date.Should().Be(Now.AddDays(-40).UtcDateTime.Date,
                $"{user.FirstName}'s oldest activity must be exactly 40 days before the seeded 'now'");
        }
    }

    [Fact]
    public async Task Seeded_users_have_more_than_one_activity_type()
    {
        await using var db = CreateContext();
        var seeder = new DataSeeder(db, new ScoringService(), new PasswordHasherAdapter(), new FixedTimeProvider(Now));

        await seeder.SeedAsync();

        var users = await db.Users.ToListAsync();
        foreach (var user in users)
        {
            var distinctTypes = await db.Activities
                .Where(a => a.UserId == user.Id)
                .Select(a => a.ActivityType)
                .Distinct()
                .CountAsync();

            distinctTypes.Should().BeGreaterThan(1, $"{user.FirstName}'s demo history should look varied, not one repeated sport");
        }
    }

    [Fact]
    public async Task Is_idempotent_and_never_produces_a_zero_or_negative_metric()
    {
        await using var db = CreateContext();
        var seeder = new DataSeeder(db, new ScoringService(), new PasswordHasherAdapter(), new FixedTimeProvider(Now));

        await seeder.SeedAsync();
        var secondRun = await seeder.SeedAsync();

        secondRun.Should().Be(0, "seeding an already-populated database must be a no-op");

        var activities = await db.Activities.ToListAsync();
        activities.Should().OnlyContain(a =>
            (!a.DistanceKm.HasValue || a.DistanceKm > 0) &&
            (!a.DurationSeconds.HasValue || a.DurationSeconds > 0) &&
            (!a.Steps.HasValue || a.Steps > 0) &&
            a.Points >= 0);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
