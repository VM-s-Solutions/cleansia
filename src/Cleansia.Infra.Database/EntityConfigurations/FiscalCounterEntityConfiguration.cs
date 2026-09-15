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

        // The allocator's atomic UPSERT keys on this index. NULLS NOT DISTINCT is vacuous on a NOT NULL
        // tenant term and is kept because the model guard reads the option, not the column.
        builder.HasIndex(c => new { c.TenantId, c.Year, c.IssuerScope })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_FiscalCounters_Tenant_Year_IssuerScope");
    }
}
