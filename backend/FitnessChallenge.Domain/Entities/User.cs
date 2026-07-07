using FitnessChallenge.Domain.Common;

namespace FitnessChallenge.Domain.Entities;

/// <summary>
/// A registered participant. Uniqueness is enforced on the normalized
/// (<see cref="NormalizedFirstName"/>, <see cref="NormalizedLastName"/>) pair — see
/// <see cref="NameNormalizer"/> for the normalization rule.
/// </summary>
public sealed class User
{
    private readonly List<Activity> _activities = new();

    // Required by EF Core materialization.
    private User() { }

    private User(Guid id, string firstName, string lastName, DateTimeOffset createdAt)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        NormalizedFirstName = NameNormalizer.Normalize(firstName);
        NormalizedLastName = NameNormalizer.Normalize(lastName);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Trimmed display first name.</summary>
    public string FirstName { get; private set; } = null!;

    /// <summary>Trimmed display last name.</summary>
    public string LastName { get; private set; } = null!;

    /// <summary>Normalized first name backing the case-insensitive unique index.</summary>
    public string NormalizedFirstName { get; private set; } = null!;

    /// <summary>Normalized last name backing the case-insensitive unique index.</summary>
    public string NormalizedLastName { get; private set; } = null!;

    /// <summary>Optional login handle — set together with <see cref="PasswordHash"/> via <see cref="SetCredentials"/>.</summary>
    public string? UserName { get; private set; }

    /// <summary>Normalized username backing the case-insensitive unique index (null when login is not enabled).</summary>
    public string? NormalizedUserName { get; private set; }

    /// <summary>Optional password hash — only set when the JWT auth feature is used.</summary>
    public string? PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Activity> Activities => _activities.AsReadOnly();

    /// <summary>Factory that trims display names and derives the normalized keys.</summary>
    public static User Register(string firstName, string lastName, DateTimeOffset createdAt, Guid? id = null)
        => new(id ?? Guid.NewGuid(), firstName.Trim(), lastName.Trim(), createdAt);

    /// <summary>
    /// Enables login by setting the username and password hash together — one without the other
    /// cannot authenticate, so the pairing is enforced here rather than left to callers.
    /// </summary>
    public void SetCredentials(string userName, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("Username is required to enable login.", nameof(userName));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required to enable login.", nameof(passwordHash));

        UserName = userName.Trim();
        NormalizedUserName = NameNormalizer.Normalize(userName);
        PasswordHash = passwordHash;
    }
}
