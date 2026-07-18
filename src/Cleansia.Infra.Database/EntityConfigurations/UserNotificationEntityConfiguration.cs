using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class UserNotificationEntityConfiguration : AuditableEntityConfiguration<UserNotification, string>
{
    public override void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        base.Configure(builder);

        builder.ToTable("UserNotifications");

        builder.Property(n => n.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(n => n.EventKey)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(n => n.ArgsJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(n => n.ReadOn)
            .IsRequired(false);

        // The per-user feed page (CreatedOn desc) and the per-user retention cap sweep.
        builder.HasIndex(n => new { n.UserId, n.CreatedOn })
            .IsDescending(false, true)
            .HasDatabaseName("IX_UserNotifications_UserId_CreatedOn");

        // Partial unread index: serves the badge count (UserId + EventKey IN keyset + unread)
        // and the digest-collapse lookup (UserId + EventKey + unread).
        builder.HasIndex(n => new { n.UserId, n.EventKey })
            .HasFilter("\"ReadOn\" IS NULL")
            .HasDatabaseName("IX_UserNotifications_UserId_EventKey_Unread");

        // The 90-day age retention sweep.
        builder.HasIndex(n => n.CreatedOn)
            .HasDatabaseName("IX_UserNotifications_CreatedOn");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
