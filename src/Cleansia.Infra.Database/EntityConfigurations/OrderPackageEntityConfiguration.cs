using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderPackageEntityConfiguration : IEntityTypeConfiguration<OrderPackage>
{
    public void Configure(EntityTypeBuilder<OrderPackage> builder)
    {
        // Restrict (not the convention Cascade) so deleting a Package that an order line references is
        // rejected at the database rather than silently stripping the line from a historical (possibly
        // invoiced/receipted) order. The admin in-use guard maps the resulting 23503 to package.in_use;
        // the DB is the final arbiter, closing the check-then-act TOCTOU window. Order -> OrderPackage
        // stays Cascade (deleting an order still removes its lines) — only the catalog side is restricted.
        builder.HasOne(op => op.Package)
            .WithMany()
            .HasForeignKey(op => op.PackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
