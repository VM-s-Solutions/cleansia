using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderReceiptEntityConfiguration : TenantAuditableEntityConfiguration<OrderReceipt, string>
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

        // The number is Format(Pattern, year, sequence) from a PER-TENANT FiscalCounter, so two
        // operators' first receipts of a year are the same string by construction (ADR-0061 D9). The
        // index is the sole arbiter between allocate and insert, so NULLS NOT DISTINCT stays on even
        // though the column is NOT NULL: the model guard reads the option, not the column.
        builder.HasIndex(r => new { r.TenantId, r.ReceiptNumber })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_OrderReceipts_TenantId_ReceiptNumber");

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
