using Cleansia.Core.Domain.EmployeePayroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class PayoutReferenceCounterEntityConfiguration : TenantAuditableEntityConfiguration<PayoutReferenceCounter, string>
{
    public override void Configure(EntityTypeBuilder<PayoutReferenceCounter> builder)
    {
        base.Configure(builder);

        builder.ToTable("PayoutReferenceCounters");

        builder.Property(e => e.Year).IsRequired();

        builder.Property(e => e.Scope)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.Value).IsRequired();

        // The allocator's atomic UPSERT keys on this index. NULLS NOT DISTINCT kept on a NOT NULL tenant
        // term -> /decisions/adr-0061#d9-nulls-not-distinct-on-every-sole-arbiter-tenant-index-and-the-two-indexes-that-gain-a-tenant-term
        builder.HasIndex(e => new { e.TenantId, e.Year, e.Scope })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_PayoutReferenceCounters_Tenant_Year_Scope");
    }
}
