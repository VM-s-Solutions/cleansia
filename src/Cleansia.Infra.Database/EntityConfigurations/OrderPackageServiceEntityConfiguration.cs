using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderPackageServiceEntityConfiguration : IEntityTypeConfiguration<OrderPackageService>
{
    public void Configure(EntityTypeBuilder<OrderPackageService> builder)
    {
        builder.Property(ops => ops.LineGross)
            .IsRequired()
            .HasPrecision(18, 2);

        // Cascade from the package line it belongs to — these are parts of that line, not rows with a
        // life of their own, and deleting an order already cascades to OrderPackages.
        builder.HasOne(ops => ops.OrderPackage)
            .WithMany(nameof(OrderPackage.IncludedServiceLines))
            .HasForeignKey(ops => ops.OrderPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict on the catalogue side, matching OrderService and OrderPackage: deleting a Service a
        // historical — possibly invoiced or receipted — order references is rejected at the database.
        //
        // The inverse is named by string for the same reason OrderService names its own:
        // Service.IncludedInOrders is a read-only .ToList() projection, so the lambda overload is
        // rejected and an unnamed .WithMany() would make EF invent a duplicate shadow FK.
        builder.HasOne(ops => ops.Service)
            .WithMany()
            .HasForeignKey(ops => ops.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per service per package line. No TenantId term: OrderPackageService derives
        // BaseEntity, so there is none to include and no global query filter attaches.
        builder.HasIndex(ops => new { ops.OrderPackageId, ops.ServiceId })
            .IsUnique()
            .HasDatabaseName("IX_OrderPackageServices_OrderPackageId_ServiceId");
    }
}
