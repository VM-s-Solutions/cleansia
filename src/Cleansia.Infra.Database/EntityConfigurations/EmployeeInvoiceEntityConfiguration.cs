using Cleansia.Core.Domain.EmployeePayroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class EmployeeInvoiceEntityConfiguration : AuditableEntityConfiguration<EmployeeInvoice, string>
{
    public override void Configure(EntityTypeBuilder<EmployeeInvoice> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.EmployeeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.PayPeriodId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.TotalOrders)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.SubTotal)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(e => e.BonusAmount)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.DeductionAmount)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.TotalAmount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(e => e.CurrencyId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasDefaultValue(Core.Domain.Enums.EmployeeInvoiceStatus.Pending);

        builder.Property(e => e.PdfBlobUrl)
            .HasMaxLength(500);

        builder.Property(e => e.GeneratedAt)
            .IsRequired();

        builder.Property(e => e.ApprovedAt)
            .IsRequired(false);

        builder.Property(e => e.ApprovedBy)
            .HasMaxLength(255);

        builder.Property(e => e.PaidAt)
            .IsRequired(false);

        builder.Property(e => e.AdminNotes)
            .HasMaxLength(1000);

        builder.Property(e => e.VariableSymbol)
            .IsRequired(false)
            .HasMaxLength(10);

        builder.Property(e => e.SpecificSymbol)
            .HasMaxLength(10);

        builder.Property(e => e.PaymentReference)
            .HasMaxLength(50);

        builder.Property(e => e.BankTransferNote)
            .HasMaxLength(500);

        // Relationships - PayPeriod relationship is configured in PayPeriodEntityConfiguration
        builder
            .HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(e => e.Currency)
            .WithMany()
            .HasForeignKey(e => e.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(e => e.Template)
            .WithMany()
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder
            .HasOne(e => e.Country)
            .WithMany()
            .HasForeignKey(e => e.CountryId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder
            .HasOne(e => e.Language)
            .WithMany()
            .HasForeignKey(e => e.LanguageId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(e => e.InvoiceNumber)
            .IsUnique();

        builder.HasIndex(e => e.VariableSymbol)
            .IsUnique()
            .HasFilter("\"VariableSymbol\" IS NOT NULL");

        builder.HasIndex(e => e.EmployeeId);
        builder.HasIndex(e => e.PayPeriodId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.EmployeeId, e.PayPeriodId })
            .IsUnique();
        builder.HasIndex(e => new { e.Status, e.GeneratedAt });
    }
}
