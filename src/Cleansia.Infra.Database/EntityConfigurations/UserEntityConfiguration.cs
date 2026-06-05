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

        // T-0106 / IDA-SEC-03: stores a SHA-256 hex hash (64 chars), not a 6-digit code.
        // (Column length / migration owned by the db agent — kept consistent on the C# side here.)
        builder.Property(u => u.ResetPasswordCode)
            .HasMaxLength(64);

        builder.Property(u => u.Profile)
            .HasConversion<int>();

        builder.Property(u => u.AuthenticationType)
            .HasConversion<int>();

        // T-0106 / IDA-SEC-03: stores a SHA-256 hex hash (64 chars), not a 6-digit code.
        // (Column length / migration owned by the db agent — kept consistent on the C# side here.)
        builder.Property(u => u.ConfirmationCode)
            .HasMaxLength(64);

        builder.Property(u => u.PreferredLanguageCode)
            .HasMaxLength(5)
            .IsRequired(false);

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

        // T-0124 (PERF-IDA-01 / PERF-IDA-05): index the identity-lookup columns so login / register /
        // password-reset / email-confirm / profile-load stop sequentially scanning Users.
        // AC1/AC3 — UNIQUE index on Email. The column is citext, so this index is natively
        // case-insensitive (no LOWER()/functional index needed). DB-level uniqueness — not just the
        // ExistsWithEmailAsync app pre-check — is the real guarantee that closes the register/update
        // TOCTOU race (PERF-IDA-05).
        builder.HasIndex(u => u.Email)
            .IsUnique();

        // AC2 — non-unique indexes on the remaining nullable lookup columns. Each is FILTERED/PARTIAL
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
    }
}