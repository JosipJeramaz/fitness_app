using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FitnessChallenge.Infrastructure.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the context without spinning up the API.
/// The runtime connection string comes from configuration in <c>Program.cs</c>.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=fitness-challenge.db")
            .Options;

        return new AppDbContext(options);
    }
}
