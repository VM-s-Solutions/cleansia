using Cleansia.Core.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CountryConfigurationEntityConfiguration : AuditableEntityConfiguration<CountryConfiguration, string>
{
    public override void Configure(EntityTypeBuilder<CountryConfiguration> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.CountryId)
            .IsRequired()
            .HasMaxLength(26);

        builder.HasOne(e => e.Country)
            .WithOne()
            .HasForeignKey<CountryConfiguration>(e => e.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CountryId)
            .IsUnique();

        builder.Property(e => e.DefaultCurrencyCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(e => e.DefaultLanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(e => e.DateFormat)
            .HasMaxLength(20);

        builder.Property(e => e.TimeZoneId)
            .HasMaxLength(50);

        builder.Property(e => e.PhonePrefix)
            .HasMaxLength(20);

        builder.Property(e => e.StandardVatRate)
            .HasPrecision(5, 4);

        builder.Property(e => e.ReducedVatRate)
            .HasPrecision(5, 4);

        builder.Property(e => e.TaxIdLabel)
            .HasMaxLength(50);

        builder.Property(e => e.TaxIdFormat)
            .HasMaxLength(100);

        builder.Property(e => e.DefaultPaymentGateway)
            .HasMaxLength(50);

        builder.Property(e => e.LegalRequirementsJson)
            .HasMaxLength(4000);
    }
}
