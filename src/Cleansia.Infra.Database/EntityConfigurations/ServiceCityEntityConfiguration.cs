using Cleansia.Core.Domain.ServiceAreas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class ServiceCityEntityConfiguration : AuditableEntityConfiguration<ServiceCity, string>
{
    public override void Configure(EntityTypeBuilder<ServiceCity> builder)
    {
        base.Configure(builder);

        builder.Property(c => c.CountryId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.ZipPrefix)
            .HasMaxLength(20);

        builder.HasOne(c => c.Country)
            .WithMany()
            .HasForeignKey(c => c.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        // The city gate (ServiceCityRepository.CityIsServicedAsync) reads one country's
        // active rows, name and ZIP prefix, and matches them in memory with
        // CityNameMatch.Serves, so this index serves that one read per country.
        //
        // Platform config (ADR-0001 Addendum A1): kept NON-unique. A case-sensitive
        // DB unique on (CountryId, Name) would contradict the case-insensitive dedup
        // the app enforces (ServiceCityRepository.ExistsWithNameInCountryAsync), and
        // there was never a tenant-bearing index here to swap.
        builder.HasIndex(c => new { c.CountryId, c.Name });

        builder.HasIndex(c => c.ZipPrefix);
    }
}
