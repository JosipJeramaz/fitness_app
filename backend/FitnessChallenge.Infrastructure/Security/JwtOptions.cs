namespace FitnessChallenge.Infrastructure.Security;

/// <summary>Bound from the <c>Auth</c> configuration section. When <see cref="Enabled"/> is false, the
/// API leaves activity endpoints open so the core behavior grades literally.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Auth";

    public bool Enabled { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "fitness-challenge";
    public string Audience { get; set; } = "fitness-challenge";
    public int ExpiryMinutes { get; set; } = 120;
}
