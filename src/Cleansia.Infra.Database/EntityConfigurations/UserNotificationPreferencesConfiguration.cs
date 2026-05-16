using Cleansia.Core.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class UserNotificationPreferencesConfiguration
    : AuditableEntityConfiguration<UserNotificationPreferences, string>
{
    public override void Configure(EntityTypeBuilder<UserNotificationPreferences> builder)
    {
        base.Configure(builder);

        builder.ToTable("UserNotificationPreferences");

        builder.Property(p => p.UserId).IsRequired();

        // One row per user — lazy-created on first GET if missing.
        builder.HasIndex(p => p.UserId).IsUnique();

        builder.Property(p => p.OrderUpdates).IsRequired().HasDefaultValue(true);
        builder.Property(p => p.CleanerOnTheWay).IsRequired().HasDefaultValue(true);
        builder.Property(p => p.OrderCompleted).IsRequired().HasDefaultValue(true);
        builder.Property(p => p.OrderCancelled).IsRequired().HasDefaultValue(true);
        builder.Property(p => p.RefundIssued).IsRequired().HasDefaultValue(true);
        builder.Property(p => p.MembershipExpiring).IsRequired().HasDefaultValue(true);
        builder.Property(p => p.MembershipCancelled).IsRequired().HasDefaultValue(true);
        builder.Property(p => p.TierUpgrade).IsRequired().HasDefaultValue(true);
        // Marketing — opt-in.
        builder.Property(p => p.Promo).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.DisputeReply).IsRequired().HasDefaultValue(true);
        builder.Property(p => p.RecurringScheduled).IsRequired().HasDefaultValue(true);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
