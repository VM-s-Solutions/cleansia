using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Database.Converters;
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

        // jsonb, not a converted text blob. Newtonsoft writes a bare enum as its integer, so the stored
        // value is `[1,3]` — which means `"Tags" @> '[12]'` answers "how many reviews complained about
        // missed areas" directly, and takes a GIN index the day the volume earns one. A text column
        // would have cost the same migration and bought none of that, which is the whole reason this is
        // a server-owned enum rather than codes prefixed onto Comment.
        //
        // The comparer is not optional: EF cannot change-track a collection behind a converter without
        // one, and its absence shows up as an Update that silently writes nothing.
        builder.Property(r => r.Tags)
            .HasColumnType("jsonb")
            .HasConversion(
                new JsonValueConverter<IReadOnlyList<ReviewTag>>(),
                new JsonValueComparer<IReadOnlyList<ReviewTag>>())
            .IsRequired();

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
