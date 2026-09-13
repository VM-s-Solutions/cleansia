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

        // One config per tier, platform-wide (ADR-0061 D7). Both readers are FirstOrDefault, so a
        // second row per tier would be an unordered pick between two thresholds.
        builder.HasIndex(c => c.Tier)
            .IsUnique();
    }
}
