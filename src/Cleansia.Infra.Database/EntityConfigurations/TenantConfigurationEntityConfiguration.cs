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

        // One value per key per tenant. NULLS NOT DISTINCT because single-tenant mode is
        // TenantId = null. The table has no writer today and that is the whole point: it costs one
        // builder call now, on a migration already being regenerated, and spares the first writer a
        // constraint that reads as enforcing while GetTenantSettingAsync picks between two
        // configured values with FirstOrDefault and no ORDER BY.
        builder.HasIndex(e => new { e.TenantId, e.Key })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
