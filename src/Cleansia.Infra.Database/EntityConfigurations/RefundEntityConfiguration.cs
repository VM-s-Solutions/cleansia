using Cleansia.Core.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class RefundEntityConfiguration : AuditableEntityConfiguration<Refund, string>
{
    public override void Configure(EntityTypeBuilder<Refund> builder)
    {
        base.Configure(builder);

        builder.ToTable("Refunds");

        builder.Property(r => r.OrderId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.ReceiptId)
            .HasMaxLength(50);

        builder.Property(r => r.DisputeId)
            .HasMaxLength(50);

        builder.Property(r => r.Amount)
            .HasPrecision(18, 2);

        builder.Property(r => r.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(r => r.RefundKey)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(r => r.Reason)
            .IsRequired();

        builder.Property(r => r.StripeRefundId)
            .HasMaxLength(255);

        builder.Property(r => r.Source)
            .IsRequired();

        builder.Property(r => r.Status)
            .IsRequired();

        builder.Property(r => r.WindowOverrideReason)
            .HasMaxLength(500);

        builder.HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Receipt)
            .WithMany()
            .HasForeignKey(r => r.ReceiptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Dispute)
            .WithMany()
            .HasForeignKey(r => r.DisputeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Collapses a concurrent double-issue onto a single winner: the loser raises 23505,
        // which the refund seam resolves to the already-recorded refund instead of double-sending.
        builder.HasIndex(r => r.RefundKey)
            .IsUnique()
            .HasDatabaseName("IX_Refunds_RefundKey");

        // Backs the refundable-ceiling read (sum of an order's succeeded refunds).
        builder.HasIndex(r => r.OrderId)
            .HasDatabaseName("IX_Refunds_OrderId");
    }
}
