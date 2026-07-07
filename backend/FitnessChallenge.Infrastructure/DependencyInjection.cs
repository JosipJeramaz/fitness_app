using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Infrastructure.Data;
using FitnessChallenge.Infrastructure.Persistence;
using FitnessChallenge.Infrastructure.Security;
using FitnessChallenge.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessChallenge.Infrastructure;

/// <summary>Composition root for the infrastructure layer (data access + security + seeding).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Data Source=fitness-challenge.db";

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        // The DbContext is the unit of work.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<ILeaderboardDataProvider, LeaderboardDataProvider>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<DataSeeder>();

        return services;
    }
}
