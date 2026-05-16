using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class PromoCodeRedemptionEntityConfiguration : AuditableEntityConfiguration<PromoCodeRedemption, string>
{
    public override void Configure(EntityTypeBuilder<PromoCodeRedemption> builder)
    {
        base.Configure(builder);

        builder.ToTable("PromoCodeRedemptions");

        builder.Property(r => r.PromoCodeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(r => r.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(r => r.OrderId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(r => r.AppliedDiscount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(r => r.RedeemedOn)
            .IsRequired();

        // Restrict on the FK so a hard-delete of a PromoCode / Order / User
        // doesn't silently drop redemption rows — we want to preserve the
        // audit trail. Admins should soft-deactivate codes via Deactivate().
        builder.HasOne(r => r.PromoCode)
            .WithMany()
            .HasForeignKey(r => r.PromoCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Per-user cap query: CountForUserAndCodeAsync(userId, codeId).
        builder.HasIndex(r => new { r.PromoCodeId, r.UserId });

        // Idempotency: GetByOrderIdAsync(orderId). Unique because we enforce
        // one redemption per order in the service layer too.
        builder.HasIndex(r => r.OrderId)
            .IsUnique();
    }
}
