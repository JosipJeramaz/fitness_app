namespace FitnessChallenge.Tests.TestDoubles;

/// <summary>A <see cref="TimeProvider"/> pinned to a fixed instant for deterministic time-window tests.</summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
