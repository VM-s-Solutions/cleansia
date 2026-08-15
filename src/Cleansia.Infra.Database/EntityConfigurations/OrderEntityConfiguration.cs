using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        // Recurring booking provenance. Plain id without a hard FK so deleting
        // a template doesn't cascade-orphan the spawned orders. Indexed because
        // the materializer queries by template to find already-spawned occurrences.
        builder.Property(o => o.RecurringTemplateId)
            .HasMaxLength(26)
            .IsRequired(false);
        builder.HasIndex(o => o.RecurringTemplateId);

        // G-18. "Did we already spawn THIS occurrence" is decided in MaterializeRecurringBookingTemplate
        // by an UNLOCKED read, and until 2026-08-15 nothing behind it enforced the answer. What actually
        // prevented a duplicate was the Azure Functions singleton timer lease — a property of the HOSTING
        // MODEL, not of the schema — so moving that sweep to any other scheduler, or fanning it out,
        // would silently have produced a second order for the same slot and, on a card template, a
        // second charge.
        //
        // This is the arbiter. The key is exactly the one the handler reasons with: the template plus the
        // exact occurrence instant, both whole minutes built by ComputeOccurrences, so equality asks the
        // handler's own question and nothing wider.
        //
        // Filtered to spawned orders because every one-off order carries RecurringTemplateId NULL and
        // they must not collide with each other. Note what the filter buys beyond that: it keeps
        // TenantId OUT of the key. A unique index containing nullable TenantId enforces nothing in
        // single-tenant mode — Postgres treats NULLs as distinct — which is the landmine CLAUDE.md
        // names, and it is why this index is on two non-null columns instead.
        builder.HasIndex(o => new { o.RecurringTemplateId, o.CleaningDateTime })
            .IsUnique()
            .HasFilter("\"RecurringTemplateId\" IS NOT NULL")
            .HasDatabaseName("IX_Orders_RecurringTemplateId_CleaningDateTime");

        // The fiscal receipt-reconciliation sweep runs 288x/day:
        //   WHERE (PaymentType = Cash OR PaymentStatus = Paid) AND CreatedOn <= cutoff ORDER BY CreatedOn
        // over the unboundedly growing Orders table. The sweep query is split into one arm per
        // eligibility clause; (PaymentStatus, CreatedOn) serves the Paid arm and (PaymentType,
        // CreatedOn) the Cash arm — each an equality + range/sort index scan instead of a seq scan.
        builder.HasIndex(o => new { o.PaymentStatus, o.CreatedOn });
        builder.HasIndex(o => new { o.PaymentType, o.CreatedOn });

        // Persisted current status, denormalized from OrderStatusHistory in Order.AddOrderStatus
        // (the single append seam). NOT NULL: every creation path appends the New track before the row
        // is staged, so a status-less order is not constructible, and the column carrying no NULLs is
        // what keeps the status term a plain equality/IN instead of an OR.
        builder.Property(o => o.CurrentStatus)
            .IsRequired();

        // (CurrentStatus, CleaningDateTime): the leftmost prefix serves the status-set predicates
        // migrated off the per-row latest-history subquery (partner available/active dashboard
        // counts, admin/partner status filters), and the second column serves the spec's most common
        // combined shape — status set + cleaning-date window — without a second index.
        builder.HasIndex(o => new { o.CurrentStatus, o.CleaningDateTime });

        // ADR-0036 — the preferred-cleaner hold deadline. Deliberately NOT indexed: the predicate's
        // satisfying set is NULL-dominant (it matches almost every row), so a partial index on the
        // non-null rows indexes exactly what the predicate excludes and could not serve it. The term is
        // a residual filter after IX_Orders_CurrentStatus_CleaningDateTime has narrowed the set.
        builder.Property(o => o.PreferredHoldUntilUtc)
            .IsRequired(false);

        // ADR-0045 — how many preferred-cleaner reservations this order has carried. NOT NULL with a
        // DATABASE default of 0, which is what makes the column additive with no backfill: every
        // pre-existing row and every writer that does not name the column reads as "no rounds spent".
        builder.Property(o => o.PreferredOfferRound)
            .IsRequired()
            .HasDefaultValue(0);

        // Stamped by the 5-minute lapse sweep and by an explicit decline, cleared by a new grant.
        // Deliberately NOT indexed, for the same reason as the deadline above: it is a residual filter
        // over the small population of orders whose reservation has just ended, and a partial index on
        // the non-null rows would index precisely what the predicate excludes.
        builder.Property(o => o.PreferredOfferLapseNotifiedAt)
            .IsRequired(false);

        // Dispute webhook lookup: charge.dispute.* events resolve the order by payment intent
        // (GetByStripePaymentIntentIdIgnoringTenantAsync) on an anonymous hot path.
        builder.HasIndex(o => o.StripePaymentIntentId);

        // UpdateCurrentUser back-fills a phone change onto historical orders matched by
        // CustomerPhone (including anonymous orders sharing the phone, so it cannot become a
        // UserId-keyed lookup without changing semantics).
        builder.HasIndex(o => o.CustomerPhone);

        // Stamped by the 24h-ahead reminder sweep. Indexed alongside
        // RecurringTemplateId so the sweep's "find Pending recurring orders
        // due in the next 24h that haven't been reminded yet" query stays cheap.
        builder.Property(o => o.RecurringReminderSentAt)
            .IsRequired(false);

        // Stamped by the T-1h pre-cleaning sweep. Deliberately NOT indexed: the sweep's leading terms are
        // CurrentStatus = Confirmed plus a fifteen-minute CleaningDateTime range, which
        // IX_Orders_CurrentStatus_CleaningDateTime serves exactly, and this column is a residual filter
        // over the handful of rows that survives it. A partial index on the non-null rows would index
        // precisely what the predicate excludes.
        builder.Property(o => o.PreCleaningReminderSentAt)
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

        // CancelledBy persists as the legacy lowercase string so already-cancelled rows
        // ("customer"/"cleaner"/"system") stay readable without a data migration — the column
        // remains varchar(20). EF's default enum-to-string would write "Customer" (capitalized)
        // and break those rows, so the mapping is explicit lowercase both ways.
        builder.Property(o => o.CancelledBy)
            .HasMaxLength(20)
            .HasConversion(new ValueConverter<CancelledBy?, string?>(
                v => v.HasValue ? v.Value.ToString().ToLowerInvariant() : null,
                v => string.IsNullOrEmpty(v) ? null : Enum.Parse<CancelledBy>(v, ignoreCase: true)));

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