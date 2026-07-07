using FitnessChallenge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessChallenge.Infrastructure.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.NormalizedFirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.NormalizedLastName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.UserName).HasMaxLength(50);
        builder.Property(u => u.NormalizedUserName).HasMaxLength(50);
        builder.Property(u => u.PasswordHash).HasMaxLength(256);
        builder.Property(u => u.CreatedAt).IsRequired();

        // Case-insensitive uniqueness of the (first, last) name pair, backed by the DB.
        builder.HasIndex(u => new { u.NormalizedFirstName, u.NormalizedLastName })
            .IsUnique()
            .HasDatabaseName("UX_Users_NormalizedName");

        // Case-insensitive username uniqueness; filtered so users without login credentials don't collide.
        builder.HasIndex(u => u.NormalizedUserName)
            .IsUnique()
            .HasFilter("\"NormalizedUserName\" IS NOT NULL")
            .HasDatabaseName("UX_Users_NormalizedUserName");

        // The read-only Activities collection is not mapped as a navigation; activities are queried by UserId.
        builder.Ignore(u => u.Activities);
    }
}
