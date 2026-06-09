using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class PackageEntityConfiguration : AuditableEntityConfiguration<Package, string>
{
    public override void Configure(EntityTypeBuilder<Package> builder)
    {
        base.Configure(builder);

        // Platform config (ADR-0001 Addendum A1): Package is identified by Id only — no
        // natural key (no Code/Slug), looked up by Id (overview / pricing GetByIds). No unique
        // index is added: there was none under tenancy and there is no natural-key column to
        // make unique. Name is display-only and may legitimately repeat.

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.Price)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(s => s.Translations)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, Translation>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, Translation>>());
    }
}