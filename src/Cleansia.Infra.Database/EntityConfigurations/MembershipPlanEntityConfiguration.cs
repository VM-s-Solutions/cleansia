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
        // MembershipPlan is platform config (ADR-0001 Addendum A1 / T-0113): not
        // tenant-scoped, so Code is unique platform-wide. The previous
        // (TenantId, Code) composite index is dropped along with the tenant
        // dimension — anonymous GetPlans no longer collapses to TenantId == null.
        builder.HasIndex(p => p.Code)
            .IsUnique();

        builder.HasIndex(p => p.IsActive);
    }
}
