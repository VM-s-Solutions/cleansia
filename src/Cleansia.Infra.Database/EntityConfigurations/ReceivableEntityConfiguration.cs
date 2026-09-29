using Cleansia.Core.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class ReceivableEntityConfiguration : TenantAuditableEntityConfiguration<Receivable, string>
{
    public override void Configure(EntityTypeBuilder<Receivable> builder)
    {
        base.Configure(builder);

        builder.ToTable("Receivables");

        builder.Property(r => r.OrderId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(r => r.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(r => r.CurrencyId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(r => r.Kind)
            .IsRequired();

        builder.Property(r => r.Amount)
            .HasPrecision(18, 2);

        builder.Property(r => r.Status)
            .IsRequired();

        builder.Property(r => r.StripePaymentIntentId)
            .HasMaxLength(255);

        builder.Property(r => r.PayLinkSessionId)
            .HasMaxLength(255);

        builder.Property(r => r.WrittenOffByUserId)
            .HasMaxLength(26);

        builder.Property(r => r.WriteOffNote)
            .HasMaxLength(500);

        builder.HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Currency)
            .WithMany()
            .HasForeignKey(r => r.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.UserId, r.Status })
            .HasDatabaseName("IX_Receivables_UserId_Status");

        builder.HasIndex(r => r.OrderId)
            .HasDatabaseName("IX_Receivables_OrderId");
    }
}
