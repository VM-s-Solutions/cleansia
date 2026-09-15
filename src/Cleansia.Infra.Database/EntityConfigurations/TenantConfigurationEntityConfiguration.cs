using Cleansia.Core.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class TenantConfigurationEntityConfiguration : TenantAuditableEntityConfiguration<TenantConfiguration, string>
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

        // One value per key per tenant. NULLS NOT DISTINCT is vacuous on a NOT NULL tenant term and is
        // kept because the model guard reads the option, not the column; GetTenantSettingAsync picks
        // between two configured values with FirstOrDefault and no ORDER BY, so the index must hold.
        builder.HasIndex(e => new { e.TenantId, e.Key })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
