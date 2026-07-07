namespace FitnessChallenge.Domain.Enums;

/// <summary>
/// The six normalized activity types the platform scores.
/// <para>
/// Important: <see cref="DailySteps"/> is NOT a wire <c>sport</c> value. It is inferred
/// from a request that omits <c>sport</c> and carries <c>steps</c> (see the validation matrix).
/// </para>
/// </summary>
public enum ActivityType
{
    Running = 0,
    Walking = 1,
    Cycling = 2,
    Gym = 3,
    Swimming = 4,
    DailySteps = 5
}
