using Cleansia.Core.Domain.EmployeePayroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderEmployeePayEntityConfiguration : AuditableEntityConfiguration<OrderEmployeePay, string>
{
    public override void Configure(EntityTypeBuilder<OrderEmployeePay> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.OrderId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.EmployeeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.PayPeriodId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.EmployeeInvoiceId)
            .HasMaxLength(26);

        builder.Property(e => e.BasePay)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(e => e.ExtrasPay)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.ExpensesPay)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.BonusPay)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.DeductionPay)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.TotalPay)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(e => e.Notes)
            .HasMaxLength(1000);

        builder.Property(e => e.PayBreakdown)
            .HasMaxLength(2000);

        builder.Property(e => e.IsApproved)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships - PayPeriod relationship is configured in PayPeriodEntityConfiguration
        builder
            .HasOne(e => e.Order)
            .WithMany()
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(e => e.EmployeeInvoice)
            .WithMany(i => i.OrderPays)
            .HasForeignKey(e => e.EmployeeInvoiceId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.OrderId);
        builder.HasIndex(e => e.EmployeeId);
        builder.HasIndex(e => e.PayPeriodId);
        builder.HasIndex(e => e.EmployeeInvoiceId);
        builder.HasIndex(e => new { e.OrderId, e.EmployeeId })
            .IsUnique();
        builder.HasIndex(e => new { e.EmployeeId, e.PayPeriodId });
    }
}
