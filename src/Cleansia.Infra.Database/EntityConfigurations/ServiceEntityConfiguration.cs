using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class ServiceEntityConfiguration : AuditableEntityConfiguration<Service, string>
{
    public override void Configure(EntityTypeBuilder<Service> builder)
    {
        base.Configure(builder);

        // Platform config (ADR-0001 Addendum A1): Service is identified by Id only —
        // it has no natural key (no Code/Slug) and is looked up by Id (overview / pricing
        // GetByIds). No unique index is added: there was none under tenancy and there is no
        // natural-key column to make unique. Name is display-only and may legitimately repeat.

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.BasePrice)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(s => s.PerRoomPrice)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(s => s.CategoryId)
            .IsRequired();

        builder.HasOne(s => s.Category)
            .WithMany(c => c.Services)
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.Translations)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, Translation>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, Translation>>());
    }
}
