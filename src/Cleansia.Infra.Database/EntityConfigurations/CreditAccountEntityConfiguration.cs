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
