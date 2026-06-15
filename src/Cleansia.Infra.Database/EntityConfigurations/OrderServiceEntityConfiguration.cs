using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderServiceEntityConfiguration : IEntityTypeConfiguration<OrderService>
{
    public void Configure(EntityTypeBuilder<OrderService> builder)
    {
        // Restrict (not the convention Cascade) so deleting a Service that an order line references is
        // rejected at the database rather than silently stripping the line from a historical (possibly
        // invoiced/receipted) order. The admin in-use guard maps the resulting 23503 to service.in_use;
        // the DB is the final arbiter, closing the check-then-act TOCTOU window. Order -> OrderService
        // stays Cascade (deleting an order still removes its lines) — only the catalog side is restricted.
        // Name the inverse navigation by string (Service.IncludedInOrders): the lambda overload
        // .WithMany(s => s.IncludedInOrders) is rejected because that property is a read-only .ToList()
        // projection, and an unnamed .WithMany() makes EF invent a duplicate shadow FK (ServiceId1). The
        // string overload binds to the existing convention-mapped navigation by metadata name.
        builder.HasOne(os => os.Service)
            .WithMany(nameof(Cleansia.Core.Domain.Services.Service.IncludedInOrders))
            .HasForeignKey(os => os.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
