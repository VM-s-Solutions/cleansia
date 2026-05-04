using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderEntityConfiguration : AuditableEntityConfiguration<Order, string>
{
    public override void Configure(EntityTypeBuilder<Order> builder)
    {
        base.Configure(builder);

        builder.Property(o => o.DisplayOrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.CustomerName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.CustomerEmail)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.CustomerPhone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(o => o.TotalPrice)
            .IsRequired()
            .HasPrecision(18, 2);

        // Loyalty tier discount applied at create-time. Nullable; not
        // required on existing/anon orders.
        builder.Property(o => o.TierDiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(o => o.TierAtPurchase);

        // Promo discount snapshot — nullable on legacy/anon orders or when
        // tier discount won the best-wins comparison.
        builder.Property(o => o.PromoDiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(o => o.PromoCodeId)
            .HasMaxLength(26)
            .IsRequired(false);

        // FK to PromoCode — Restrict so an admin can't hard-delete a code
        // that's referenced by historical orders (preserves receipt rendering
        // and audit linkage). Use Deactivate() instead.
        builder.HasOne<PromoCode>()
            .WithMany()
            .HasForeignKey(o => o.PromoCodeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Membership discount snapshot — nullable on legacy/anon orders or when
        // tier/promo won the best-wins comparison.
        builder.Property(o => o.MembershipDiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(o => o.MembershipPlanIdAtPurchase)
            .HasMaxLength(26)
            .IsRequired(false);

        // No FK on MembershipPlanIdAtPurchase — kept as a snapshot string
        // (like TierAtPurchase) so plan deletions don't cascade or block.

        // Customer-requested cleaner. Stored as a plain id without an FK to
        // Employee — the matching service interprets it as a hint, and we
        // don't want a hard FK that would break if the employee is removed.
        builder.Property(o => o.PreferredEmployeeId)
            .HasMaxLength(26)
            .IsRequired(false);

        builder.Property(o => o.Extras)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, bool>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, bool>>());

        builder.Property(o => o.ConfirmationCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.StripeSessionId)
            .IsRequired()
            .HasMaxLength(100);

        // EF Core must write to the private backing fields because the public
        // OrderNotes / OrderIssues properties return ReadOnlyCollections.
        builder.Navigation(o => o.OrderNotes)
            .HasField("_notes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.OrderIssues)
            .HasField("_issues")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne(o => o.Receipt)
            .WithOne(r => r.Order)
            .HasForeignKey<OrderReceipt>(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}