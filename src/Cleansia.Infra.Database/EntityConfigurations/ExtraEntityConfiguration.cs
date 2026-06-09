using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class ExtraEntityConfiguration : AuditableEntityConfiguration<Extra, string>
{
    public override void Configure(EntityTypeBuilder<Extra> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.Slug)
            .IsRequired()
            .HasMaxLength(50);

        // Platform config (ADR-0001 Addendum A1): Slug is unique platform-wide.
        // The anonymous overview and the pricing path look extras up by slug, so a
        // duplicate would surface non-deterministic results. The previous
        // (TenantId, Slug) composite is dropped with the tenant dimension.
        builder.HasIndex(e => e.Slug).IsUnique();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        builder.Property(e => e.Price)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(e => e.DisplayOrder)
            .IsRequired();

        builder.Property(e => e.Translations)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, Translation>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, Translation>>());
    }
}
