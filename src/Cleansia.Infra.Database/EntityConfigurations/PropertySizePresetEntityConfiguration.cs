using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class PropertySizePresetEntityConfiguration
    : AuditableEntityConfiguration<PropertySizePreset, string>
{
    public override void Configure(EntityTypeBuilder<PropertySizePreset> builder)
    {
        base.Configure(builder);

        builder.Property(p => p.CountryId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(p => p.SortOrder).IsRequired();
        builder.Property(p => p.Rooms).IsRequired();
        builder.Property(p => p.Bathrooms).IsRequired();

        builder.Property(p => p.Translations)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, Translation>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, Translation>>());

        // Catalogue data, not tenant data — like Country and ServiceCity — so the
        // natural key is (CountryId, Code) with no TenantId term. That also keeps it
        // clear of the single-tenant NULL trap: a unique index containing a nullable
        // TenantId enforces nothing while that column is null.
        // → /architecture/security-rules
        builder.HasIndex(p => new { p.CountryId, p.Code })
            .IsUnique()
            .HasDatabaseName("IX_PropertySizePresets_CountryId_Code");

        // The list is always read for one country in display order.
        builder.HasIndex(p => new { p.CountryId, p.SortOrder })
            .HasDatabaseName("IX_PropertySizePresets_CountryId_SortOrder");

        builder.HasOne(p => p.Country)
            .WithMany()
            .HasForeignKey(p => p.CountryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
