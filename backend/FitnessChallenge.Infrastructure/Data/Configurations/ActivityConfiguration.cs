using FitnessChallenge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessChallenge.Infrastructure.Data.Configurations;

public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("Activities");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.DateTimeUtc).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        // Store the enum as a readable string in the DB (nice for the demo SQLite file); still filterable.
        builder.Property(a => a.ActivityType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Leave the default decimal->TEXT mapping: the SQLite provider stores decimals as text, preserving
        // exact precision (e.g. 42.195). A "decimal(10,3)" column type would give NUMERIC affinity and risk
        // silent REAL/float storage.
        builder.Property(a => a.DistanceKm);
        builder.Property(a => a.DurationSeconds);
        builder.Property(a => a.Steps);
        builder.Property(a => a.Points).IsRequired();

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Covers the hot paths: per-user reads and the leaderboard's "<= asOf" scans.
        builder.HasIndex(a => new { a.UserId, a.DateTimeUtc }).HasDatabaseName("IX_Activities_User_DateTime");
        builder.HasIndex(a => a.DateTimeUtc).HasDatabaseName("IX_Activities_DateTime");
    }
}
