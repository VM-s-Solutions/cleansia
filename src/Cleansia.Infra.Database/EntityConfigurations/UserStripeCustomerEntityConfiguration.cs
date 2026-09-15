using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class UserStripeCustomerEntityConfiguration : TenantAuditableEntityConfiguration<UserStripeCustomer, string>
{
    public override void Configure(EntityTypeBuilder<UserStripeCustomer> builder)
    {
        base.Configure(builder);

        builder.ToTable("UserStripeCustomers");

        builder.Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(c => c.CurrencyId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(c => c.StripeCustomerId)
            .IsRequired()
            .HasMaxLength(64);

        // Restrict both ways: a Stripe Customer that billed a user is a record neither a user erasure
        // nor a currency delete may silently drop. CurrencyRepository.IsInUseAsync names this table so
        // DeleteCurrency answers currency.in_use rather than a raw 23503.
        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Currency)
            .WithMany()
            .HasForeignKey(c => c.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // One Customer per user per currency. No TenantId term on purpose: a user belongs to one
        // tenant, so the pair is already tenant-unique.
        builder.HasIndex(c => new { c.UserId, c.CurrencyId })
            .IsUnique()
            .HasDatabaseName("IX_UserStripeCustomers_UserId_CurrencyId");

        // A Stripe Customer is single-currency, so it belongs to exactly one row. The resolver checks
        // before adopting the legacy Customer; this is the backstop.
        builder.HasIndex(c => c.StripeCustomerId)
            .IsUnique()
            .HasDatabaseName("IX_UserStripeCustomers_StripeCustomerId");
    }
}
