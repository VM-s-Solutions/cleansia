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
        // signing key, so two DIFFERENT users on the same physical
        // device legitimately produce two rows.
        //
        // The index spans active AND inactive rows (no IsActive filter).
        // A logout soft-deletes (IsActive=false) but keeps the row, so a
        // SAME-user re-login would collide on re-INSERT. RegisterDevice
        // therefore looks up INCLUDING inactive rows
        // (GetByUserAndDeviceIdIncludingInactiveAsync) and RECLAIMS the
        // tombstone via Device.MarkRegistered (reactivate + refresh token)
        // — never a second INSERT — so there is at most one row per
        // (UserId, DeviceId) and the retention sweep (which only prunes
        // IsActive rows) never needs to reclaim it.
        builder.HasIndex(d => d.UserId);
        builder.HasIndex(d => new { d.UserId, d.DeviceId }).IsUnique();

        // The stale-device retention sweep (DataRetentionBackgroundService.CleanStaleDevicesAsync)
        // filters IsActive AND LastActiveAt < cutoff over every tenant. This (IsActive, LastActiveAt)
        // composite serves that equality + range so the sweep is index-backed, not a full scan.
        builder.HasIndex(d => new { d.IsActive, d.LastActiveAt });

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
