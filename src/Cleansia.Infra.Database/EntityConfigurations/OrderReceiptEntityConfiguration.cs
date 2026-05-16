using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderReceiptEntityConfiguration : AuditableEntityConfiguration<OrderReceipt, string>
{
    public override void Configure(EntityTypeBuilder<OrderReceipt> builder)
    {
        base.Configure(builder);

        builder.ToTable("OrderReceipts");

        builder.Property(r => r.ReceiptNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.IssuedAt).IsRequired();

        builder.Property(r => r.FileName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.BlobName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.EmailSent).IsRequired();

        builder.Property(r => r.EmailMessageId).HasMaxLength(255);

        builder.HasOne(r => r.Language)
            .WithMany()
            .HasForeignKey(r => r.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.ReceiptNumber)
            .IsUnique()
            .HasDatabaseName("IX_OrderReceipts_ReceiptNumber");

        builder.HasIndex(r => new { r.OrderId, r.LanguageId })
            .HasDatabaseName("IX_OrderReceipts_Order_Language");

        builder.Property(r => r.FiscalProviderKey).HasMaxLength(50);
        builder.Property(r => r.FiscalCode).HasMaxLength(255);
        builder.Property(r => r.FiscalError).HasMaxLength(1000);
        builder.Property(r => r.FiscalErrorKind).HasConversion<int?>();

        // Index used by the retry job — filtered to due rows only.
        builder.HasIndex(r => r.FiscalNextRetryAt)
            .HasDatabaseName("IX_OrderReceipts_FiscalNextRetryAt")
            .HasFilter("\"FiscalNextRetryAt\" IS NOT NULL");
    }
}
