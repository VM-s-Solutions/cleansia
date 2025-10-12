using Cleansia.Core.Domain.InvoiceTemplates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CountryInvoiceConfigEntityConfiguration : BaseEntityConfiguration<CountryInvoiceConfig, string>
{
    public override void Configure(EntityTypeBuilder<CountryInvoiceConfig> builder)
    {
        base.Configure(builder);

        builder.ToTable("CountryInvoiceConfigs");

        builder.Property(e => e.VatRequired)
            .IsRequired();

        builder.Property(e => e.VatRate)
            .IsRequired()
            .HasPrecision(5, 4);

        builder.Property(e => e.DigitalSignatureRequired)
            .IsRequired();

        builder.Property(e => e.EInvoiceFormat)
            .HasMaxLength(50);

        builder.Property(e => e.AdditionalFieldsJson)
            .HasMaxLength(2000);

        builder.Property(e => e.LegalDisclaimerTemplate)
            .HasMaxLength(500);

        builder.HasOne(e => e.Country)
            .WithMany()
            .HasForeignKey(e => e.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CountryId)
            .IsUnique();
    }
}
