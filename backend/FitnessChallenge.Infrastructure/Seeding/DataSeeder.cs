using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Domain.Entities;
using FitnessChallenge.Domain.Enums;
using FitnessChallenge.Domain.Scoring;
using FitnessChallenge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessChallenge.Infrastructure.Seeding;

/// <summary>
/// Populates an instantly-usable demo: five users with 40 days of varied, competitive activity history
/// (every user's oldest record is exactly 40 days old), so the leaderboard trend and the "All" race chart
/// both show real lead changes instead of one runaway winner. Each user has a distinct storyline —
/// steady climber, early leader who fades then rallies, reliable mid-pack, bursty/erratic, late bloomer —
/// driven by a per-user intensity curve over time plus day-to-day jitter. Idempotent; deterministic
/// (fixed RNG seed per user) so re-seeding a cleared database reproduces the same demo.
/// Demo login: username is <c>first.last</c> (e.g. <c>ada.lovelace</c>), password is <c>password123</c> for every user.
/// </summary>
public sealed class DataSeeder
{
    public const string DemoPassword = "password123";
    private const int HistoryDays = 40;

    private readonly AppDbContext _db;
    private readonly IScoringService _scoring;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _clock;

    public DataSeeder(AppDbContext db, IScoringService scoring, IPasswordHasher passwordHasher, TimeProvider clock)
    {
        _db = db;
        _scoring = scoring;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    public async Task<int> SeedAsync(CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(ct))
            return 0;

        var now = _clock.GetUtcNow();
        var passwordHash = _passwordHasher.Hash(DemoPassword);

        foreach (var profile in Profiles)
        {
            var user = User.Register(profile.First, profile.Last, now.AddDays(-(HistoryDays + 5)));
            user.SetCredentials($"{profile.First.ToLowerInvariant()}.{profile.Last.ToLowerInvariant()}", passwordHash);
            await _db.Users.AddAsync(user, ct);

            var rng = new Random(profile.Seed);
            foreach (var (timestamp, type, distance, seconds, steps) in GenerateActivities(profile, rng, now))
            {
                var points = _scoring.CalculatePoints(type, distance, seconds, steps);
                await _db.Activities.AddAsync(
                    Activity.Create(user.Id, timestamp, type, distance, seconds, steps, points, now), ct);
            }
        }

        return await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Walks from <see cref="HistoryDays"/> ago (oldest — always active, guaranteeing that exact age for
    /// every user) to today. On other days, the user trains with probability <see cref="UserProfile.ActiveChance"/>;
    /// when they do, one sport is drawn from their weighted preferences and its metric scaled by
    /// <see cref="UserProfile.Intensity"/> at that point in time, so each storyline actually shapes the totals.
    /// </summary>
    private static IEnumerable<(DateTimeOffset Timestamp, ActivityType Type, decimal? Distance, int? Seconds, int? Steps)> GenerateActivities(
        UserProfile profile, Random rng, DateTimeOffset now)
    {
        for (var daysAgo = HistoryDays; daysAgo >= 0; daysAgo--)
        {
            if (daysAgo != HistoryDays && rng.NextDouble() > profile.ActiveChance)
                continue;

            var intensity = profile.Intensity(daysAgo);
            var jitter = 0.85 + rng.NextDouble() * 0.3; // +/-15% day-to-day noise
            var day = now.AddDays(-daysAgo);
            var timestamp = new DateTimeOffset(day.Year, day.Month, day.Day, 6 + rng.Next(15), rng.Next(60), 0, TimeSpan.Zero);
            if (timestamp > now)
                timestamp = now;

            var sport = WeightedPick(profile.Sports, rng);
            yield return sport switch
            {
                ActivityType.Gym => (timestamp, sport, null, (int)(2400 * intensity * jitter), (int?)null),
                ActivityType.Swimming => (timestamp, sport, null, (int)(1800 * intensity * jitter), null),
                ActivityType.DailySteps => (timestamp, sport, null, null, (int)(8000 * intensity * jitter)),
                _ => (timestamp, sport, Math.Round(BaseKm(sport) * (decimal)(intensity * jitter), 2), null, null),
            };
        }
    }

    private static decimal BaseKm(ActivityType type) => type switch
    {
        ActivityType.Running => 7m,
        ActivityType.Cycling => 22m,
        _ => 3.5m // Walking
    };

    private static ActivityType WeightedPick(IReadOnlyList<(ActivityType Type, double Weight)> sports, Random rng)
    {
        var roll = rng.NextDouble() * sports.Sum(s => s.Weight);
        var cumulative = 0.0;
        foreach (var (type, weight) in sports)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return type;
        }
        return sports[^1].Type;
    }

    private sealed record UserProfile(
        string First,
        string Last,
        int Seed,
        double ActiveChance,
        IReadOnlyList<(ActivityType Type, double Weight)> Sports,
        Func<int, double> Intensity);

    // Progress: 0.0 at HistoryDays ago (oldest) -> 1.0 today. Intensity curves read left-to-right in time.
    private static double Progress(int daysAgo) => 1.0 - daysAgo / (double)HistoryDays;
    private static double Lerp(double from, double to, double t) => from + (to - from) * Math.Clamp(t, 0, 1);

    private static readonly UserProfile[] Profiles =
    {
        // Steady climber: builds form across the full 40 days and is still on the rise at the end.
        new("Ada", "Lovelace", Seed: 101, ActiveChance: 0.6,
            Sports: new[] { (ActivityType.Running, 0.65), (ActivityType.Walking, 0.2), (ActivityType.Cycling, 0.15) },
            Intensity: daysAgo => Lerp(0.6, 1.3, Progress(daysAgo))),

        // Fast starter who fades mid-block, then rallies hard in the final stretch — a real head-to-head with Ada.
        new("Alan", "Turing", Seed: 202, ActiveChance: 0.6,
            Sports: new[] { (ActivityType.Cycling, 0.6), (ActivityType.Gym, 0.4) },
            Intensity: daysAgo =>
            {
                var p = Progress(daysAgo);
                return p < 0.55 ? Lerp(1.35, 0.75, p / 0.55) : Lerp(0.75, 1.6, (p - 0.55) / 0.45);
            }),

        // Reliable mid-pack: gentle upward drift, no dramatic swings.
        new("Grace", "Hopper", Seed: 303, ActiveChance: 0.55,
            Sports: new[] { (ActivityType.Swimming, 0.6), (ActivityType.Walking, 0.4) },
            Intensity: daysAgo => Lerp(0.8, 1.1, Progress(daysAgo))),

        // Bursty: frequent rest days punctuated by big step/run days — volatile totals, unpredictable rank.
        new("Linus", "Torvalds", Seed: 404, ActiveChance: 0.5,
            Sports: new[] { (ActivityType.DailySteps, 0.6), (ActivityType.Running, 0.4) },
            Intensity: daysAgo => 0.7 + 1.1 * Math.Pow(Math.Sin(daysAgo * 0.9), 2)),

        // Late bloomer: quiet first three weeks, then a hard comeback that closes the gap on the leaders.
        new("Margaret", "Hamilton", Seed: 505, ActiveChance: 0.6,
            Sports: new[]
            {
                (ActivityType.Running, 0.3), (ActivityType.Cycling, 0.25),
                (ActivityType.Swimming, 0.25), (ActivityType.Gym, 0.1), (ActivityType.DailySteps, 0.1)
            },
            Intensity: daysAgo =>
            {
                var p = Progress(daysAgo);
                return p < 0.5 ? 0.4 : Lerp(0.4, 1.7, (p - 0.5) / 0.5);
            }),
    };
}
