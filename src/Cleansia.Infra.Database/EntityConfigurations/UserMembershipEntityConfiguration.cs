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
    }
}
