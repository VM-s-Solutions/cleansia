using Cleansia.Core.Domain.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class FiscalCounterEntityConfiguration : TenantAuditableEntityConfiguration<FiscalCounter, string>
{
    public override void Configure(EntityTypeBuilder<FiscalCounter> builder)
    {
        base.Configure(builder);

        builder.ToTable("FiscalCounters");

        builder.Property(c => c.Year).IsRequired();

        builder.Property(c => c.IssuerScope)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Value).IsRequired();

        // The allocator's atomic UPSERT keys on this index. NULLS NOT DISTINCT kept on a NOT NULL tenant
        // term -> /decisions/adr-0061#d9-nulls-not-distinct-on-every-sole-arbiter-tenant-index-and-the-two-indexes-that-gain-a-tenant-term
        builder.HasIndex(c => new { c.TenantId, c.Year, c.IssuerScope })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_FiscalCounters_Tenant_Year_IssuerScope");
    }
}
