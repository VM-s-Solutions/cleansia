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

        // The clamp bounds captured at pay-calculation time (T-0362). 0 == unbounded on that edge.
        builder.Property(e => e.MinPay)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(e => e.MaxPay)
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
            .WithMany(emp => emp.OrderPays)
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(e => e.EmployeeInvoice)
            .WithMany(i => i.OrderPays)
            .HasForeignKey(e => e.EmployeeInvoiceId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // THE UNIT OF THE EIGHT MONEY COLUMNS. Required, and FK-backed with Restrict for the same
        // reason EmployeePayConfigs and EmployeeInvoices restrict: deleting a currency that a cleaner's
        // recorded pay is denominated in would leave those amounts meaning nothing, and the row is an
        // input to a tax document. CurrencyRepository.IsInUseAsync is the friendly refusal in front of
        // it; this is what happens if anything gets past that.
        builder.Property(e => e.CurrencyId)
            .IsRequired()
            .HasMaxLength(26);

        builder
            .HasOne<Cleansia.Core.Domain.Internationalization.Currency>()
            .WithMany()
            .HasForeignKey(e => e.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.OrderId);
        builder.HasIndex(e => e.EmployeeId);
        builder.HasIndex(e => e.PayPeriodId);
        builder.HasIndex(e => e.EmployeeInvoiceId);
        builder.HasIndex(e => new { e.OrderId, e.EmployeeId })
            .IsUnique();
        builder.HasIndex(e => new { e.EmployeeId, e.PayPeriodId });
    }
}
