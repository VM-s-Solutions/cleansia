using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Internationalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CompanyInfoEntityConfiguration : AuditableEntityConfiguration<CompanyInfo, string>
{
    public override void Configure(EntityTypeBuilder<CompanyInfo> builder)
    {
        base.Configure(builder);

        builder.ToTable("CompanyInfo");

        builder.Property(c => c.LegalName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.TradingName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Tagline).HasMaxLength(500);
        builder.Property(c => c.RegistrationNumber).IsRequired().HasMaxLength(50);
        builder.Property(c => c.VatNumber).HasMaxLength(50);
        builder.Property(c => c.Street).IsRequired().HasMaxLength(100);
        builder.Property(c => c.City).IsRequired().HasMaxLength(100);
        builder.Property(c => c.ZipCode).IsRequired().HasMaxLength(20);
        builder.Property(c => c.CountryId).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(50);
        builder.Property(c => c.Email).HasMaxLength(100);
        builder.Property(c => c.Website).HasMaxLength(200);
        builder.Property(c => c.BankName).HasMaxLength(100);
        builder.Property(c => c.BankAccountNumber).HasMaxLength(50);
        builder.Property(c => c.Iban).HasMaxLength(50);
        builder.Property(c => c.Swift).HasMaxLength(20);

        builder.HasOne(c => c.Country)
            .WithMany()
            .HasForeignKey(c => c.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        // (RegistrationNumber, CountryId), not RegistrationNumber alone.
        //
        // ONE LEGAL ENTITY MAY HOLD A ROW PER COUNTRY. Owner ruling 2026-09-08 is "one entity", with the
        // caveat that it is hard to answer before the first one exists — and the composite accommodates
        // both futures while the global unique accommodates neither:
        //
        //   one entity, several countries -> same registration number, different CountryId. Allowed
        //     here; FORBIDDEN by the index this replaces, even though the read path
        //     (OrderFactory, ReceiptService, RegenerateInvoicePdf) has always been per-country.
        //   two entities, one country each -> different registration numbers. Allowed by both.
        //
        // So it is strictly more permissive and forecloses nothing, which is why it can land before the
        // business answer does. A second ACTIVE company per country is still refused — by
        // CreateCompanyInfo's validator, which is where that rule has always lived.
        builder.HasIndex(c => new { c.RegistrationNumber, c.CountryId })
            .IsUnique()
            .HasDatabaseName("IX_CompanyInfo_RegistrationNumber_CountryId");
        builder.HasIndex(c => c.IsActive).HasDatabaseName("IX_CompanyInfo_IsActive");

        // Index on CountryId and IsActive for faster lookups (uniqueness enforced at application level)
        builder.HasIndex(c => new { c.CountryId, c.IsActive })
            .HasDatabaseName("IX_CompanyInfo_CountryId_IsActive");
    }
}
