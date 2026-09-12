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

        // ONE ACCOUNT PER CUSTOMER PER CURRENCY. Unique on (UserId, CurrencyId), and deliberately not
        // (TenantId, UserId, CurrencyId): TenantId is nullable and Postgres treats NULLs as distinct,
        // so a composite index containing it admits unlimited duplicates while it is null, which is
        // production today. Both columns here are NOT NULL, so this is a real constraint with nothing
        // to fold — no .AreNullsDistinct(false) needed. User is already tenant-scoped.
        //
        // UserId leads so the same index also serves the currency-BLIND reads: "every account this
        // customer holds", which is what the GDPR erasure gate and the admin discharge must ask.
        //
        // The 1:1 it replaces was not merely restrictive, it was wrong: a balance is denominated, and
        // one row per customer forced every currency's money into whichever one the customer's first
        // credit happened to open. The repository's own doc already promised the opposite — "an
        // existing account keeps the currency it was opened in" — while the query behind it matched on
        // UserId alone and handed back an account in a different currency for the caller to add to.
        builder.HasIndex(a => new { a.UserId, a.CurrencyId })
            .IsUnique();

        // 1:MANY, with no collection navigation on User: nothing walks the graph from a user to their
        // accounts — every such question is a repository query — and an unnamed .WithMany() onto a
        // navigation that does not exist is how a duplicate shadow FK gets invented.
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
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
