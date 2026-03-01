using Cleansia.Core.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class TenantConfigurationEntityConfiguration : AuditableEntityConfiguration<TenantConfiguration, string>
{
    public override void Configure(EntityTypeBuilder<TenantConfiguration> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Value)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(e => e.Description)
            .HasMaxLength(200);

        builder.Property(e => e.Category)
            .HasMaxLength(50);

        builder.HasIndex(e => new { e.TenantId, e.Key })
            .IsUnique();
    }
}
