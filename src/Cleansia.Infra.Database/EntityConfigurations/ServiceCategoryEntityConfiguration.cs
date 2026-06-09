using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class ServiceCategoryEntityConfiguration : AuditableEntityConfiguration<ServiceCategory, string>
{
    public override void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        base.Configure(builder);

        builder.Property(c => c.Slug)
            .IsRequired()
            .HasMaxLength(50);

        // Platform config (ADR-0001 Addendum A1): Slug is unique platform-wide,
        // not per tenant — the previous (TenantId, Slug) composite is dropped
        // along with the tenant dimension so anonymous catalog reads no longer
        // collapse to the TenantId == null slice.
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.DisplayOrder)
            .IsRequired();

        builder.Property(c => c.Translations)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, Translation>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, Translation>>());
    }
}
