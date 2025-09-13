using Cleansia.Core.Domain.Internalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CountryEntityConfiguration : AuditableEntityConfiguration<Country, string>
{
    public override void Configure(EntityTypeBuilder<Country> builder)
    {
        base.Configure(builder);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Translations)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, Translation>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, Translation>>());
    }
}