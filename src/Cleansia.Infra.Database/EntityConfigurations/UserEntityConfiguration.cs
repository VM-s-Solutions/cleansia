using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class UserEntityConfiguration : TenantAuditableEntityConfiguration<User, string>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);

        builder.Property(u => u.Password)
            .HasMaxLength(255)
            .HasConversion(new PasswordConverter());

        builder.Property(u => u.FirstName)
            .HasColumnType("citext")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.LastName)
            .HasColumnType("citext")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.Email)
            .HasColumnType("citext")
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(u => u.PhoneNumber)
            .HasColumnType("citext")
            .HasMaxLength(50);

        builder.Property(u => u.GoogleId)
            .HasMaxLength(512);

        builder.Property(u => u.AppleId)
            .HasMaxLength(512);

        // Stores a SHA-256 hex hash (64 chars), not a 6-digit code.
        // (Column length / migration owned by the db agent — kept consistent on the C# side here.)
        builder.Property(u => u.ResetPasswordCode)
            .HasMaxLength(64);

        builder.Property(u => u.Profile)
            .HasConversion<int>();

        builder.Property(u => u.AdminRole)
            .HasConversion<int?>();

        // The role is an axis of the Administrator profile alone: an administrator row without one and
        // a customer or cleaner row with one are both refused at the database, not only by the factory.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Users_AdminRole_Profile",
            "(\"Profile\" = 100) = (\"AdminRole\" IS NOT NULL)"));

        builder.Property(u => u.AuthenticationType)
            .HasConversion<int>();

        // Stores a SHA-256 hex hash (64 chars), not a 6-digit code.
        // (Column length / migration owned by the db agent — kept consistent on the C# side here.)
        builder.Property(u => u.ConfirmationCode)
            .HasMaxLength(64);

        builder.Property(u => u.PreferredLanguageCode)
            .HasMaxLength(5)
            .IsRequired(false);

        builder.Property(u => u.LastLoginAt)
            .IsRequired(false);

        builder.Property(u => u.FailedLoginAttempts)
            .HasDefaultValue(0);

        builder.Property(u => u.LockoutEndsAt)
            .IsRequired(false);

        builder.Property(u => u.ConfirmationCodeAttempts)
            .HasDefaultValue(0);

        builder.Property(u => u.ResetPasswordCodeAttempts)
            .HasDefaultValue(0);

        builder
            .HasOne(u => u.PreferredLanguage)
            .WithMany()
            .HasForeignKey(u => u.PreferredLanguageCode)
            .HasPrincipalKey(l => l.Code)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder
            .HasMany(u => u.Orders)
            .WithOne(o => o.User)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Identity-lookup indexes. UNIQUE on Email, globally: one identity per email across the holding
        // (ADR-0061 D5.1) — citext, so natively case-insensitive. Every anonymous identity read (login,
        // lockout, reset, OTP confirm, social link-by-email) resolves by email ignoring the tenant, so a
        // per-tenant scope would make login ambiguous the day a second operator opens. DB-level
        // uniqueness, NOT the app pre-check, is what closes the register TOCTOU race; the four writers
        // own the 23505 (ADR-0050 D2). → /architecture/security-rules
        builder.HasIndex(u => u.Email)
            .IsUnique();

        // Non-unique indexes on the remaining nullable lookup columns. Each is FILTERED/PARTIAL
        // (WHERE "Col" IS NOT NULL, using the real PascalCase Postgres column names) so the (typically
        // many) null rows are not indexed.
        builder.HasIndex(u => u.PhoneNumber)
            .HasFilter("\"PhoneNumber\" IS NOT NULL");

        builder.HasIndex(u => u.ConfirmationCode)
            .HasFilter("\"ConfirmationCode\" IS NOT NULL");

        builder.HasIndex(u => u.ResetPasswordCode)
            .HasFilter("\"ResetPasswordCode\" IS NOT NULL");

        builder.HasIndex(u => u.GoogleId)
            .HasFilter("\"GoogleId\" IS NOT NULL");

        builder.HasIndex(u => u.AppleId)
            .HasFilter("\"AppleId\" IS NOT NULL");
    }
}