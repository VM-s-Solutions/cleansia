using Cleansia.Core.Domain.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class FiscalCounterEntityConfiguration : AuditableEntityConfiguration<FiscalCounter, string>
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

        // The allocator's atomic UPSERT keys on this index. NULLS NOT DISTINCT so a single-tenant
        // (null TenantId) deployment collapses onto ONE counter row per (Year, IssuerScope) — a plain
        // unique index would treat each null as distinct and let duplicates in, breaking gaplessness.
        builder.HasIndex(c => new { c.TenantId, c.Year, c.IssuerScope })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_FiscalCounters_Tenant_Year_IssuerScope");
    }
}
