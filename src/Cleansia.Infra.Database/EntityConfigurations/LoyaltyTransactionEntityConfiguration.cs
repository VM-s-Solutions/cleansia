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

        // T-0112 (LG-SEC-06 / S7a) — client-supplied idempotency key for the
        // manual admin grant/revoke path. Nullable; the filtered unique index
        // below is the atomic backstop that collapses a double-submit.
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

        // T-0112 (LG-SEC-06 / S7a + S8) — FILTERED, TENANT-SCOPED unique index on
        // the manual-grant idempotency key. LoyaltyTransaction is ITenantEntity, so
        // per S8 ("unique indexes on tenant-scoped tables are (TenantId, X), not (X)")
        // the key is unique PER TENANT, not globally — matching PromoCode / ReferralCode
        // / PromoCodeRedemption (T-0110). The requestId is a CLIENT-generated token, so
        // two different tenants could legitimately produce the same value; a bare global
        // unique index would wrongly collapse tenant B's grant onto tenant A's row
        // (cross-tenant false-positive / leak). The fast-path read GetByIdempotencyKeyAsync
        // already runs through the global tenant query filter, so the DB backstop must match
        // that grain.
        //
        // Filtered (WHERE "IdempotencyKey" IS NOT NULL) so the existing order-driven /
        // referral rows (NULL key, every tenant) are unaffected and back-compat is
        // preserved. In MULTI-tenant mode (TenantId NOT NULL) it is the atomic backstop
        // that rejects a concurrent double-submit within a tenant (Postgres 23505), which
        // LoyaltyService catches and collapses to the same success.
        //
        // SINGLE-TENANT / back-compat caveat (TenantId == NULL): Postgres treats NULLs in a
        // UNIQUE index as DISTINCT by default, so two NULL-tenant rows with the SAME key are
        // NOT rejected — the DB concurrency backstop is relaxed when TenantId is NULL. This is
        // the SAME tradeoff every other tenant-scoped unique index in this repo already makes
        // (PromoCode/ReferralCode (TenantId, Code), PromoCodeRedemption (TenantId, ...)); we
        // stay consistent rather than introduce a one-off NULLS NOT DISTINCT. The SERIAL-replay
        // fast-path read (GetByIdempotencyKeyAsync) still collapses double-submits in single-
        // tenant mode; only the true-concurrent NULL-tenant race degrades to the order-driven
        // guard's level. See the schema-delta MANUAL_STEP for the NULLS-NOT-DISTINCT upgrade
        // option if single-tenant concurrency hardening is ever required.
        //
        // Owner-only ef-migration emits this as a partial unique index.
        builder.HasIndex(t => new { t.TenantId, t.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
    }
}
