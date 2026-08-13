using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class UserEntityConfiguration : AuditableEntityConfiguration<User, string>
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

        // Identity-lookup indexes. UNIQUE on Email — citext, so natively case-insensitive.
        // DB-level uniqueness, NOT the app pre-check, is what closes the register/update TOCTOU race.
        //
        // Scope is (TenantId, Email), never global Email: a global unique index made tenant B's
        // registration 500 on an unhandled 23505 when tenant A already held the address — a
        // cross-tenant existence oracle. → /architecture/security-rules
        builder.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique()
            .AreNullsDistinct(false);

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