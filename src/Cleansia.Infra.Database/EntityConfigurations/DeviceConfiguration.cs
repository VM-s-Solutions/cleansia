using Cleansia.Core.Domain.Devices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class DeviceConfiguration : AuditableEntityConfiguration<Device, string>
{
    public override void Configure(EntityTypeBuilder<Device> builder)
    {
        base.Configure(builder);

        builder.ToTable("Devices");

        builder.Property(d => d.UserId).IsRequired();
        builder.Property(d => d.Platform).IsRequired().HasMaxLength(10);
        builder.Property(d => d.DeviceToken).IsRequired().HasMaxLength(512);
        builder.Property(d => d.DeviceId).IsRequired().HasMaxLength(256);
        builder.Property(d => d.LastActiveAt).IsRequired();
        builder.Property(d => d.NotificationsEnabled).IsRequired().HasDefaultValue(true);

        // Uniqueness is scoped to (UserId, DeviceId), NOT to DeviceId
        // alone. ANDROID_ID — the source of DeviceId on Android — is
        // stable across app reinstalls and shared across the same
        // signing key, so two different users on the same physical
        // device legitimately produce two rows. The previous global
        // unique on DeviceId alone caused INSERTs to collide when a
        // different user signed in on a handset where a stale row from
        // a prior account still existed; the handler's per-(user,
        // device) lookup couldn't see the old row, tried to insert,
        // and tripped the index. Orphan rows from prior users
        // accumulate but no longer block; a cleanup job by
        // LastActiveAt can prune them later.
        builder.HasIndex(d => d.UserId);
        builder.HasIndex(d => new { d.UserId, d.DeviceId }).IsUnique();

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
