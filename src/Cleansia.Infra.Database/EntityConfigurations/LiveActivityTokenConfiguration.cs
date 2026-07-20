using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class LiveActivityTokenConfiguration : AuditableEntityConfiguration<LiveActivityToken, string>
{
    public override void Configure(EntityTypeBuilder<LiveActivityToken> builder)
    {
        base.Configure(builder);

        builder.ToTable("LiveActivityTokens");

        builder.Property(t => t.UserId).IsRequired().HasMaxLength(26);
        builder.Property(t => t.DeviceId).IsRequired().HasMaxLength(256);
        builder.Property(t => t.OrderId).HasMaxLength(26);
        builder.Property(t => t.Token).IsRequired().HasMaxLength(512);
        builder.Property(t => t.LastUpdatedAt).IsRequired();

        // Registration upserts on this key (ActivityKit rotates tokens mid-activity and across
        // installs). NULLS NOT DISTINCT so the per-install push-to-start token (null OrderId) collapses
        // onto ONE row per (UserId, DeviceId) — a plain unique index treats each null as distinct and
        // would let a second push-to-start row in, breaking last-write-wins for that token.
        builder.HasIndex(t => new { t.UserId, t.DeviceId, t.OrderId })
            .IsUnique()
            .AreNullsDistinct(false);

        // The producer gate and the consumer's per-order token resolution read by (UserId, OrderId).
        builder.HasIndex(t => new { t.UserId, t.OrderId });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
