using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class LoyaltyTransactionEntityConfiguration : AuditableEntityConfiguration<LoyaltyTransaction, string>
{
    public override void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        base.Configure(builder);

        builder.ToTable("LoyaltyTransactions");

        builder.Property(t => t.LoyaltyAccountId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(t => t.Type)
            .IsRequired();

        builder.Property(t => t.Points)
            .IsRequired();

        builder.Property(t => t.Source)
            .IsRequired();

        builder.Property(t => t.OrderId)
            .HasMaxLength(26);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.OccurredOn)
            .IsRequired();

        // Optional FK to Order — Restrict so completed orders aren't
        // hard-deletable while their loyalty ledger entries exist.
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(t => t.OrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Activity feed: order by OccurredOn DESC, scoped to an account.
        builder.HasIndex(t => t.OccurredOn)
            .IsDescending();

        builder.HasIndex(t => new { t.LoyaltyAccountId, t.OccurredOn })
            .IsDescending(false, true);

        // Idempotency lookup: GetLatestForOrderSourceAsync(OrderId, Source)
        builder.HasIndex(t => new { t.OrderId, t.Source });
    }
}
