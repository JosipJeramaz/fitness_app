using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Domain.Entities;
using FitnessChallenge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitnessChallenge.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct)
        => _db.Users.AsNoTracking().AnyAsync(u => u.Id == id, ct);

    public Task<bool> ExistsByNormalizedNameAsync(string normalizedFirstName, string normalizedLastName, CancellationToken ct)
        => _db.Users.AsNoTracking()
            .AnyAsync(u => u.NormalizedFirstName == normalizedFirstName && u.NormalizedLastName == normalizedLastName, ct);

    public Task<bool> ExistsByNormalizedUserNameAsync(string normalizedUserName, CancellationToken ct)
        => _db.Users.AsNoTracking().AnyAsync(u => u.NormalizedUserName == normalizedUserName, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByNormalizedNameAsync(string normalizedFirstName, string normalizedLastName, CancellationToken ct)
        => _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedFirstName == normalizedFirstName && u.NormalizedLastName == normalizedLastName, ct);

    public Task<User?> GetByNormalizedUserNameAsync(string normalizedUserName, CancellationToken ct)
        => _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, ct);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct)
        => await _db.Users.AsNoTracking()
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct)
        => await _db.Users.AddAsync(user, ct);
}
