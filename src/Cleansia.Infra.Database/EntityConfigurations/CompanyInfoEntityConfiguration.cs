using Cleansia.Core.Domain.Company;
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

        builder.HasIndex(c => c.RegistrationNumber).IsUnique().HasDatabaseName("IX_CompanyInfo_RegistrationNumber");
        builder.HasIndex(c => c.IsActive).HasDatabaseName("IX_CompanyInfo_IsActive");
    }
}
