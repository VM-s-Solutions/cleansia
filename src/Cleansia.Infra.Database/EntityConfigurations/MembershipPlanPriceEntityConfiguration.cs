using Cleansia.Core.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class MembershipPlanPriceEntityConfiguration : AuditableEntityConfiguration<MembershipPlanPrice, string>
{
    public override void Configure(EntityTypeBuilder<MembershipPlanPrice> builder)
    {
        base.Configure(builder);

        builder.ToTable("MembershipPlanPrices");

        builder.Property(p => p.Price).IsRequired().HasPrecision(18, 2);

        builder.Property(p => p.StripePriceId)
            .IsRequired()
            .HasMaxLength(64);

        // Plan -> price is Cascade, currency -> price is Restrict: the PackagePrice shape, for the reasons
        // recorded there. CurrencyRepository.IsInUseAsync names this table so DeleteCurrency answers
        // currency.in_use rather than a raw 23503.
        builder.HasOne(p => p.MembershipPlan)
            .WithMany()
            .HasForeignKey(p => p.MembershipPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Currency)
            .WithMany()
            .HasForeignKey(p => p.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // One price per plan per currency. No TenantId term: the plan is platform config
        // (MembershipPlanPlatformConfigStructuralTests), as PackagePrices.
        builder.HasIndex(p => new { p.MembershipPlanId, p.CurrencyId })
            .IsUnique()
            .HasDatabaseName("IX_MembershipPlanPrices_MembershipPlanId_CurrencyId");

        // A Stripe Price is single-currency and single-product, so two rows naming one is an admin typo.
        // The validator refuses it first (membership.plan.stripe_price_already_used); this is the backstop.
        builder.HasIndex(p => p.StripePriceId)
            .IsUnique()
            .HasDatabaseName("IX_MembershipPlanPrices_StripePriceId");
    }
}
