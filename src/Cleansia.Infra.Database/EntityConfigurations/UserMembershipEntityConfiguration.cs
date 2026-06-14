using Cleansia.Core.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class UserMembershipEntityConfiguration : AuditableEntityConfiguration<UserMembership, string>
{
    public override void Configure(EntityTypeBuilder<UserMembership> builder)
    {
        base.Configure(builder);

        builder.ToTable("UserMemberships");

        builder.Property(m => m.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(m => m.MembershipPlanId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(m => m.StripeSubscriptionId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(m => m.Status)
            .IsRequired();

        builder.Property(m => m.CurrentPeriodStart)
            .IsRequired();

        builder.Property(m => m.CurrentPeriodEnd)
            .IsRequired();

        builder.Property(m => m.CancelledAt);

        builder.Property(m => m.RenewalReminderSentAt);
        builder.Property(m => m.CancellationReminderSentAt);

        // FK to User. Restrict delete: Stripe subscription must be cancelled
        // before the user can be deleted, otherwise we'd lose the audit trail.
        builder.HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to MembershipPlan. Restrict delete: a plan can't be hard-deleted
        // while subscriptions reference it; admin must mark IsActive=false instead.
        builder.HasOne(m => m.MembershipPlan)
            .WithMany()
            .HasForeignKey(m => m.MembershipPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // Webhook reconciliation looks up by StripeSubscriptionId; keep unique.
        builder.HasIndex(m => m.StripeSubscriptionId)
            .IsUnique();

        // Pricing pipeline does "active membership for user" lookups on every
        // CreateOrder. Composite index keeps that O(log n).
        builder.HasIndex(m => new { m.UserId, m.Status });

        // The membership lifecycle cron sweep (SendMembershipLifecycleNotifications) selects by
        // Status = Active AND CurrentPeriodEnd IN [range] AND RenewalReminderSentAt IS NULL with NO
        // UserId, so the (UserId, Status) index above can't seek. This (Status, CurrentPeriodEnd)
        // composite serves the sweep's equality + range. Partial on the un-reminded rows keeps it tiny.
        builder.HasIndex(m => new { m.Status, m.CurrentPeriodEnd })
            .HasFilter("\"RenewalReminderSentAt\" IS NULL");

        // ADR-0002 D2 — DB-level backstop for the
        // "at most one ACTIVE membership per user" invariant. The webhook
        // provisioning path (StripeSubscriptionWebhookHandler
        // .ProvisionFromCreatedEventAsync) asserts via GetActiveForUserAsync
        // before Create, but a check-then-insert is a TOCTOU race (S7a); this
        // FILTERED partial unique index makes Postgres reject a second active
        // row (SQLSTATE 23505) even when two webhooks race past the read.
        //
        // CRITICAL: the index is FILTERED to Status = Active so a cancelled /
        // expired membership PLUS a new active subscription is still allowed —
        // a naive full (TenantId, UserId) unique would wrongly block the
        // legitimate re-subscribe-after-cancel case (see UserMembership.cs).
        // Status is mapped as its underlying int (no string conversion above),
        // and MembershipStatus.Active = 1, so the partial-index predicate is
        // "Status" = 1.
        //
        // Tenant-scoped (TenantId, UserId) per S8 — UserMembership is an
        // ITenantEntity. NOTE: Postgres treats NULLs as DISTINCT in a UNIQUE
        // index by default, so two NULL-TenantId active rows for the same user
        // are NOT rejected by this index (single-tenant mode); there the
        // app-level GetActiveForUserAsync assert + the StripeSubscriptionId
        // unique index are the guards, and the index hardens multi-tenant mode.
        // This is the SAME tradeoff every other tenant-scoped unique index in
        // this repo makes (LoyaltyTransaction (TenantId, IdempotencyKey),
        // PromoCode/ReferralCode (TenantId, Code)); we stay consistent rather
        // than introduce a one-off NULLS NOT DISTINCT.
        //
        // Owner-only ef-migration emits this as a partial unique index.
        builder.HasIndex(m => new { m.TenantId, m.UserId })
            .IsUnique()
            .HasFilter("\"Status\" = 1");
    }
}
