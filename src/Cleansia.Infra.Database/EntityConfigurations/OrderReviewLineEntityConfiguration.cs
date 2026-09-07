using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderReviewLineEntityConfiguration : AuditableEntityConfiguration<OrderReviewLine, string>
{
    public override void Configure(EntityTypeBuilder<OrderReviewLine> builder)
    {
        base.Configure(builder);

        builder.ToTable("OrderReviewLines");

        builder.Property(l => l.OrderReviewId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(l => l.ServiceId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(l => l.PackageId)
            .HasMaxLength(26);

        builder.Property(l => l.Rating)
            .IsRequired();

        builder.HasOne(l => l.Review)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.OrderReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Cleansia.Core.Domain.Services.Service>()
            .WithMany()
            .HasForeignKey(l => l.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Cleansia.Core.Domain.Packages.Package>()
            .WithMany()
            .HasForeignKey(l => l.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        // One score per item per review — SetLines replaces wholesale, so a duplicate here would be
        // a bug rather than an edit. NULLS NOT DISTINCT because PackageId is nullable and the
        // constraint would otherwise admit unlimited duplicate standalone-service rows.
        builder.HasIndex(l => new { l.OrderReviewId, l.ServiceId, l.PackageId })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
