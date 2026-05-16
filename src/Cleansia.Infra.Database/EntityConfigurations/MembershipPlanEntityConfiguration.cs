using Cleansia.Core.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class MembershipPlanEntityConfiguration : AuditableEntityConfiguration<MembershipPlan, string>
{
    public override void Configure(EntityTypeBuilder<MembershipPlan> builder)
    {
        base.Configure(builder);

        builder.ToTable("MembershipPlans");

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.MonthlyPriceCzk)
            .HasPrecision(18, 2);

        builder.Property(p => p.StripePriceId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(p => p.DiscountPercentage)
            .HasPrecision(5, 2);

        builder.Property(p => p.FreeCancellationWindowHours)
            .IsRequired();

        builder.Property(p => p.AllowsExpressUpgrade)
            .IsRequired();

        builder.Property(p => p.BillingInterval)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.TrialPeriodDays)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired();

        // Computed property — exclude from EF mapping.
        builder.Ignore(p => p.MonthlyEquivalentPriceCzk);

        // Code is referenced by handler logic ("look up the PLUS_MONTHLY plan").
        // Tenant-scoped to keep the option open for tenant-specific plan
        // catalogues, even though we'll likely have a single global Plus plan.
        builder.HasIndex(p => new { p.TenantId, p.Code })
            .IsUnique();

        builder.HasIndex(p => p.IsActive);
    }
}
