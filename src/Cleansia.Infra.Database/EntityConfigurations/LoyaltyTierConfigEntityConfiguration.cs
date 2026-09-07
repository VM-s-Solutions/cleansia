using Cleansia.Core.Domain.Loyalty;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class LoyaltyTierConfigEntityConfiguration : AuditableEntityConfiguration<LoyaltyTierConfig, string>
{
    public override void Configure(EntityTypeBuilder<LoyaltyTierConfig> builder)
    {
        base.Configure(builder);

        builder.ToTable("LoyaltyTierConfigs");

        builder.Property(c => c.Tier)
            .IsRequired();

        builder.Property(c => c.LifetimePointsThreshold)
            .IsRequired();

        builder.Property(c => c.DiscountPercent)
            .IsRequired()
            .HasPrecision(5, 4);

        builder.Property(c => c.MinimumOrderAmountForDiscount)
            .HasPrecision(18, 2);

        builder.Property(c => c.PerksJson)
            .IsRequired()
            .HasMaxLength(2000);

        // One config per tier per tenant.
        // One config per tier per tenant. NULLS NOT DISTINCT because single-tenant mode IS
        // TenantId = null: without it the constraint reads as enforcing while admitting N rows per
        // tier. No application writer exists today, which is exactly why it is worth fixing now —
        // both readers are FirstOrDefault, so the first writer added would inherit an unordered pick
        // between two thresholds and two discount percentages.
        builder.HasIndex(c => new { c.TenantId, c.Tier })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
