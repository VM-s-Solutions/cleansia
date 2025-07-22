using Cleansia.Core.Domain.Internalization;
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

        builder.Property(s => s.Translations)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, Translation>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, Translation>>());
    }
}