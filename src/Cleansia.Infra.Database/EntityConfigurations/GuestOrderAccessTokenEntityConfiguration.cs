using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class GuestOrderAccessTokenEntityConfiguration
    : TenantAuditableEntityConfiguration<GuestOrderAccessToken, string>
{
    public override void Configure(EntityTypeBuilder<GuestOrderAccessToken> builder)
    {
        base.Configure(builder);

        builder.Property(t => t.OrderId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(t => t.ExpiresOn)
            .IsRequired();

        builder.HasOne(t => t.Order)
            .WithMany()
            .HasForeignKey(t => t.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // The only lookup path: the anonymous endpoints hash the presented token and resolve the
        // booking by this column alone, so it must be unique and index-served.
        builder.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_GuestOrderAccessTokens_TokenHash");

        // Supersede-on-reissue and revoke-on-cancellation both read an order's live tokens.
        builder.HasIndex(t => new { t.OrderId, t.RevokedOn })
            .HasDatabaseName("IX_GuestOrderAccessTokens_OrderId_RevokedOn");
    }
}
