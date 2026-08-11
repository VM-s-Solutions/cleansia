using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class EmployeePayoutDetailsEntityConfiguration : AuditableEntityConfiguration<EmployeePayoutDetails, string>
{
    public override void Configure(EntityTypeBuilder<EmployeePayoutDetails> builder)
    {
        base.Configure(builder);

        builder.ToTable("EmployeePayoutDetails");

        builder.Property(p => p.EmployeeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(p => p.Scheme)
            .HasConversion<int>();

        builder.Property(p => p.BankCountryId)
            .HasMaxLength(26);

        // char(6) / char(10): the stored form is zero-padded canonical, so every value is exactly the
        // declared width and a fixed-length column makes a short write visible instead of silent.
        builder.Property(p => p.AccountPrefix)
            .HasMaxLength(6)
            .IsFixedLength();

        builder.Property(p => p.AccountNumber)
            .HasMaxLength(10)
            .IsFixedLength();

        builder.Property(p => p.BankCode)
            .HasMaxLength(4);

        builder.Property(p => p.Iban)
            .HasMaxLength(34);

        builder.Property(p => p.Swift)
            .HasMaxLength(11);

        builder.Property(p => p.BankName)
            .HasMaxLength(100);

        builder.Property(p => p.HolderName)
            .HasMaxLength(200);

        builder.Property(p => p.ProviderAccountRef)
            .HasMaxLength(100);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.RevealCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Cascade: the payout destination has no meaning without its employee, and a tombstoned
        // destination is the compliance defect (ADR-0034 D1.1.2) — erasure deletes rather than deactivates.
        builder.HasOne(p => p.Employee)
            .WithOne(e => e.PayoutDetails)
            .HasForeignKey<EmployeePayoutDetails>(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: a country referenced by a live payout destination cannot be hard-deleted.
        builder.HasOne(p => p.BankCountry)
            .WithMany()
            .HasForeignKey(p => p.BankCountryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cardinality one, enforced (ADR-0034 D1.3). NULLS NOT DISTINCT because single-tenant mode IS
        // TenantId = null: a plain unique index treats each null as distinct and would let a second
        // payout destination in for the same cleaner in the platform's default deployment. Unfiltered —
        // nothing deactivates this row, so there is no partial-index interaction to reason about.
        builder.HasIndex(p => new { p.TenantId, p.EmployeeId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_EmployeePayoutDetails_Tenant_Employee");
    }
}
