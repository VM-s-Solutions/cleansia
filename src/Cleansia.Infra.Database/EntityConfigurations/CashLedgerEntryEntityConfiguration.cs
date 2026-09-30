using Cleansia.Core.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CashLedgerEntryEntityConfiguration : TenantAuditableEntityConfiguration<CashLedgerEntry, string>
{
    public override void Configure(EntityTypeBuilder<CashLedgerEntry> builder)
    {
        base.Configure(builder);

        builder.ToTable("CashLedgerEntries");

        builder.Property(e => e.EmployeeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.OrderId)
            .HasMaxLength(26);

        builder.Property(e => e.CurrencyId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.Kind)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasPrecision(18, 2);

        builder.Property(e => e.Note)
            .HasMaxLength(500);

        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Order)
            .WithMany()
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Currency)
            .WithMany()
            .HasForeignKey(e => e.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.EmployeeId, e.CurrencyId })
            .HasDatabaseName("IX_CashLedgerEntries_EmployeeId_CurrencyId");

        // One collection per order: two taps of "collected" racing past the validator must not count the
        // cash twice against the cleaner.
        builder.HasIndex(e => e.OrderId)
            .IsUnique()
            .HasFilter("\"OrderId\" IS NOT NULL")
            .HasDatabaseName("IX_CashLedgerEntries_OrderId");
    }
}
