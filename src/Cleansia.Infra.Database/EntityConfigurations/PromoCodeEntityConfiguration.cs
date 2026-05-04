using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class PromoCodeEntityConfiguration : AuditableEntityConfiguration<PromoCode, string>
{
    public override void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        base.Configure(builder);

        builder.ToTable("PromoCodes");

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(p => p.Type)
            .IsRequired();

        builder.Property(p => p.DiscountPercent)
            .HasPrecision(5, 4);

        builder.Property(p => p.DiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(p => p.MinimumOrderAmount)
            .HasPrecision(18, 2);

        builder.Property(p => p.MaxRedemptionsPerUser)
            .IsRequired();

        builder.Property(p => p.GlobalMaxRedemptions);

        builder.Property(p => p.CurrentRedemptionsCount)
            .IsRequired();

        builder.Property(p => p.ValidFrom);
        builder.Property(p => p.ValidUntil);

        builder.Property(p => p.IsActive)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.CurrencyId)
            .HasMaxLength(26)
            .IsRequired(false);

        // Optional FK to Currency for fixed-amount codes; null for percent codes.
        builder.HasOne(p => p.Currency)
            .WithMany()
            .HasForeignKey(p => p.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Lookup is GetByCodeAsync(code) — codes are tenant-scoped, hence the
        // composite unique index. Tenant filter still applies at the query
        // level via the global EF filter.
        builder.HasIndex(p => new { p.TenantId, p.Code })
            .IsUnique();

        // IsActive is a hot filter for "all active codes" admin views.
        builder.HasIndex(p => p.IsActive);

        // Active-window scans for cron jobs / admin dashboards.
        builder.HasIndex(p => new { p.ValidFrom, p.ValidUntil });
    }
}
