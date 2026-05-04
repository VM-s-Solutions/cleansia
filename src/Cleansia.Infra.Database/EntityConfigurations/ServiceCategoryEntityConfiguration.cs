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

        builder.HasIndex(c => new { c.TenantId, c.Slug }).IsUnique();

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
