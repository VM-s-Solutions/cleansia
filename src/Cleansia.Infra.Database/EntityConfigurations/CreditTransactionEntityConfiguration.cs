using Cleansia.Core.Domain.Credit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CreditTransactionEntityConfiguration : AuditableEntityConfiguration<CreditTransaction, string>
{
    public override void Configure(EntityTypeBuilder<CreditTransaction> builder)
    {
        base.Configure(builder);

        builder.ToTable("CreditTransactions");

        builder.Property(t => t.CreditAccountId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(t => t.Amount)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(t => t.Reason)
            .IsRequired();

        builder.Property(t => t.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(t => t.OrderId).HasMaxLength(26);
        builder.Property(t => t.DisputeId).HasMaxLength(26);
        builder.Property(t => t.Note).HasMaxLength(500);

        // A PLAIN unique index on a NOT NULL column, with no filter and no tenant term. The key is
        // minted per grant and unique on its own, and this table carries no tenant column, so there is
        // nothing to add. This is money; the backstop has to actually fire, so there is nothing here
        // that can be null and nothing to filter on — the shape that once let LoyaltyTransactions'
        // (TenantId, IdempotencyKey) index enforce nothing while its tenant term was nullable.
        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique();

        builder.HasIndex(t => t.CreditAccountId);
    }
}
