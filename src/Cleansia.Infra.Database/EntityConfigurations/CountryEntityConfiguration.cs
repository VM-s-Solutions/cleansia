using Cleansia.Core.Domain.Internationalization;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore;
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

        builder.Property(c => c.IsoCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(s => s.Translations)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, Translation>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, Translation>>());

        builder
            .HasMany(c => c.Employees)
            .WithOne(e => e.Nationality)
            .HasForeignKey(e => e.NationalityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}