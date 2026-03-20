using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderReviewEntityConfiguration : AuditableEntityConfiguration<OrderReview, string>
{
    public override void Configure(EntityTypeBuilder<OrderReview> builder)
    {
        base.Configure(builder);

        builder.ToTable("OrderReviews");

        builder.Property(r => r.OrderId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(r => r.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(r => r.Rating)
            .IsRequired();

        builder.Property(r => r.Comment)
            .HasMaxLength(1000);

        builder.HasOne(r => r.Order)
            .WithMany(o => o.Reviews)
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.OrderId)
            .HasDatabaseName("IX_OrderReviews_OrderId");

        builder.HasIndex(r => new { r.OrderId, r.UserId })
            .IsUnique()
            .HasDatabaseName("IX_OrderReviews_OrderId_UserId");
    }
}
