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
        // NULLS NOT DISTINCT is what makes this the arbiter its own name claims. Single-tenant mode
        // is TenantId = null, so a nulls-distinct index never fires and
        // FlushCollapsingUniqueViolationAsync — which exists solely to catch this index's 23505 —
        // never runs. The live callers are server-generated deterministic keys, not a UI:
        // ForceQualifyReferral, ReverseReferral, and the partial-refund clawback keyed on
        // RefundService's refundKey, which is driven from Stripe webhooks and therefore genuinely
        // retried and genuinely concurrent.
        //
        // The TENANT TERM STAYS: the key is a caller-supplied token, so a bare global index would
        // read a cross-tenant collision as this tenant's own replay and silently swallow a real
        // grant. The FILTER STAYS too — order-completion earns carry a null key, and NULLS NOT
        // DISTINCT without the filter would collapse every one of them onto a single key and cap the
        // platform at one such earn per tenant, ever.
        builder.HasIndex(t => new { t.TenantId, t.IdempotencyKey })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
    }
}
