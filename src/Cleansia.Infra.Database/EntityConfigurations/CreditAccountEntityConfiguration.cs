using Cleansia.Core.Domain.Credit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CreditAccountEntityConfiguration : AuditableEntityConfiguration<CreditAccount, string>
{
    public override void Configure(EntityTypeBuilder<CreditAccount> builder)
    {
        base.Configure(builder);

        builder.ToTable("CreditAccounts");

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(26);

        // The scale the rest of the platform's money uses.
        builder.Property(a => a.Balance)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(a => a.CurrencyId)
            .IsRequired()
            .HasMaxLength(26);

        // Nullable: an account that has never moved has no clock running. Indexed and FILTERED,
        // because the sweep's only query is "everything past its date" and the rows it must never
        // consider are exactly the null ones - a partial index keeps it off every account that has
        // been emptied or never used.
        builder.HasIndex(a => a.ExpiresOn)
            .HasFilter("\"ExpiresOn\" IS NOT NULL");

        // 1:1 with User, unique on UserId ALONE — matching LoyaltyAccount, and deliberately not
        // (TenantId, UserId). TenantId is nullable and Postgres treats NULLs as distinct, so a
        // composite index containing it admits unlimited duplicates while it is null, which is
        // production today. User is already tenant-scoped, so this one is the real constraint.
        builder.HasIndex(a => a.UserId)
            .IsUnique();

        builder.HasOne(a => a.User)
            .WithOne()
            .HasForeignKey<CreditAccount>(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Transactions)
            .WithOne(t => t.Account)
            .HasForeignKey(t => t.CreditAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.Transactions)
            .HasField("_transactions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
