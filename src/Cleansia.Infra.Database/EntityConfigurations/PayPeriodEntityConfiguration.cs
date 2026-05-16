using Cleansia.Core.Domain.EmployeePayroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class PayPeriodEntityConfiguration : AuditableEntityConfiguration<PayPeriod, string>
{
    public override void Configure(EntityTypeBuilder<PayPeriod> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.StartDate)
            .IsRequired();

        builder.Property(e => e.EndDate)
            .IsRequired();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasDefaultValue(Core.Domain.Enums.PayPeriodStatus.Open);

        builder.Property(e => e.ClosedAt)
            .IsRequired(false);

        builder.Property(e => e.ClosedBy)
            .HasMaxLength(255);

        builder.Property(e => e.PaidAt)
            .IsRequired(false);

        builder.Property(e => e.Notes)
            .HasMaxLength(1000);

        // Relationships - PayPeriod has navigation collections, so configure from here
        builder
            .HasMany(p => p.OrderPays)
            .WithOne(o => o.PayPeriod)
            .HasForeignKey(o => o.PayPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(p => p.Invoices)
            .WithOne(i => i.PayPeriod)
            .HasForeignKey(i => i.PayPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.StartDate, e.EndDate });
        builder.HasIndex(e => e.StartDate);
        builder.HasIndex(e => e.EndDate);
    }
}
