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

        // T-0110 — 0-based per-user redemption slot. Backs the per-user unique cap below.
        builder.Property(r => r.SlotOrdinal)
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

        // T-0110 / S8 — tenant-scoped UNIQUE per-user redemption slot. This is the
        // defense-in-depth BACKSTOP for the atomic slot reservation in
        // PromoCodeRedemptionRepository.TryReserveRedemptionSlotAsync: it hard-caps the per-user
        // redemptions at MaxRedemptionsPerUser distinct ordinals while keeping M>1 codes valid
        // (slots 0..M-1). Tenant-scoped per S8 — a code is unique per tenant, so two tenants share
        // independent ordinal spaces. Replaces the old non-unique (PromoCodeId, UserId) index; this
        // unique index still serves CountForUserAndCodeAsync's (PromoCodeId, UserId) lookup as a
        // left-prefix.
        builder.HasIndex(r => new { r.TenantId, r.PromoCodeId, r.UserId, r.SlotOrdinal })
            .IsUnique();

        // Idempotency: GetByOrderIdAsync(orderId). Unique because we enforce
        // one redemption per order in the service layer too. KEEP per T-0110 — collapses
        // same-order double-fire (audit trail) independently of the per-user slot cap.
        builder.HasIndex(r => r.OrderId)
            .IsUnique();
    }
}
