using Cleansia.Core.Domain.Devices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.UserId).IsRequired();
        builder.Property(d => d.Platform).IsRequired().HasMaxLength(10);
        builder.Property(d => d.DeviceToken).IsRequired().HasMaxLength(512);
        builder.Property(d => d.DeviceId).IsRequired().HasMaxLength(256);
        builder.Property(d => d.LastActiveAt).IsRequired();

        builder.HasIndex(d => d.DeviceId).IsUnique();
        builder.HasIndex(d => d.UserId);
        builder.HasIndex(d => new { d.UserId, d.DeviceId }).IsUnique();

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
