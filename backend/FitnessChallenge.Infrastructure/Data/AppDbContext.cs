using FitnessChallenge.Application.Abstractions;
using FitnessChallenge.Domain.Entities;
using FitnessChallenge.Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;

namespace FitnessChallenge.Infrastructure.Data;

/// <summary>EF Core context. Also serves as the unit of work (its SaveChanges is the commit boundary).</summary>
public sealed class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Activity> Activities => Set<Activity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every DateTimeOffset is persisted as UTC ticks so SQLite can compare/order/index it.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToTicksConverter>();
        base.ConfigureConventions(configurationBuilder);
    }
}
