using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class RefreshTokenEntityConfiguration : AuditableEntityConfiguration<RefreshToken, string>
{
    public override void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        base.Configure(builder);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(t => t.ExpiresAt)
            .IsRequired();

        builder.Property(t => t.RevokedReason)
            .HasMaxLength(20);

        builder.Property(t => t.ReplacedByTokenId)
            .HasMaxLength(26);

        builder.Property(t => t.DeviceLabel)
            .HasMaxLength(120);

        builder.Property(t => t.DeviceId)
            .HasMaxLength(64);

        builder.Property(t => t.IpAddress)
            .HasMaxLength(45);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Primary lookup path: refresh endpoint hashes the incoming raw token and
        // queries by TokenHash. Must be unique + fast.
        builder.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_TokenHash");

        // Secondary lookup: "revoke all active tokens for user" + audit queries.
        builder.HasIndex(t => new { t.UserId, t.RevokedAt })
            .HasDatabaseName("IX_RefreshTokens_UserId_RevokedAt");

        // Cleanup job: "expired tokens older than N days".
        builder.HasIndex(t => t.ExpiresAt)
            .HasDatabaseName("IX_RefreshTokens_ExpiresAt");
    }
}
