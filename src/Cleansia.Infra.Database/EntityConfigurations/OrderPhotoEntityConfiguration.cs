using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderPhotoEntityConfiguration : AuditableEntityConfiguration<OrderPhoto, string>
{
    public override void Configure(EntityTypeBuilder<OrderPhoto> builder)
    {
        base.Configure(builder);

        builder.ToTable("OrderPhotos");

        builder.Property(p => p.OrderId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(p => p.PhotoType)
            .IsRequired();

        builder.Property(p => p.BlobUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.OriginalFileName)
            .HasMaxLength(255);

        builder.Property(p => p.ContentType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.CapturedAt)
            .IsRequired();

        builder.Property(p => p.CapturedByEmployeeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(p => p.Notes)
            .HasMaxLength(500);

        // Relationships
        builder.HasOne(p => p.Order)
            .WithMany(o => o.Photos)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.CapturedBy)
            .WithMany()
            .HasForeignKey(p => p.CapturedByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(p => p.OrderId)
            .HasDatabaseName("IX_OrderPhotos_OrderId");

        builder.HasIndex(p => new { p.OrderId, p.PhotoType })
            .HasDatabaseName("IX_OrderPhotos_Order_PhotoType");

        builder.HasIndex(p => p.CapturedByEmployeeId)
            .HasDatabaseName("IX_OrderPhotos_CapturedByEmployeeId");
    }
}
