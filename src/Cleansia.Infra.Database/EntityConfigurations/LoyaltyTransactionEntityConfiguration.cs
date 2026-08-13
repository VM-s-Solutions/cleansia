using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class LoyaltyTransactionEntityConfiguration : AuditableEntityConfiguration<LoyaltyTransaction, string>
{
    public override void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        base.Configure(builder);

        builder.ToTable("LoyaltyTransactions");

        builder.Property(t => t.LoyaltyAccountId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(t => t.Type)
            .IsRequired();

        builder.Property(t => t.Points)
            .IsRequired();

        builder.Property(t => t.Source)
            .IsRequired();

        builder.Property(t => t.OrderId)
            .HasMaxLength(26);

        // Client-supplied idempotency key for the keyed paths (the manual admin grant/revoke and the
        // per-refund partial clawback, which keys on the refund key). Nullable; the filtered unique index
        // below is the atomic backstop that collapses a concurrent double-submit.
        builder.Property(t => t.IdempotencyKey)
            .HasMaxLength(80);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.OccurredOn)
            .IsRequired();

        // Optional FK to Order — Restrict so completed orders aren't
        // hard-deletable while their loyalty ledger entries exist.
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(t => t.OrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Activity feed: order by OccurredOn DESC, scoped to an account.
        builder.HasIndex(t => t.OccurredOn)
            .IsDescending();

        builder.HasIndex(t => new { t.LoyaltyAccountId, t.OccurredOn })
            .IsDescending(false, true);

        // Idempotency lookup: GetLatestForOrderSourceAsync(OrderId, Source)
        builder.HasIndex(t => new { t.OrderId, t.Source });

        // FILTERED, TENANT-SCOPED unique index on the manual-grant idempotency key. The requestId is a
        // CLIENT token, so two tenants can legitimately produce the same value — a bare GLOBAL unique
        // index would collapse tenant B's grant onto tenant A's row. Filtered on NOT NULL so the
        // order-driven and referral rows (NULL key) are unaffected. → /architecture/security-rules
        builder.HasIndex(t => new { t.TenantId, t.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
    }
}
