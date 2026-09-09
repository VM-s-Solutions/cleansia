using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderExtraEntityConfiguration : IEntityTypeConfiguration<OrderExtra>
{
    public void Configure(EntityTypeBuilder<OrderExtra> builder)
    {
        // The catalogue slug as it stood at purchase. Capped at 50 to match Extra.Slug — and the cap is
        // load-bearing beyond tidiness: SubjectDataErasureRosterTests treats any entity declaring an
        // uncapped non-key string column as carrying unbounded text, and would red with a GDPR failure
        // that names nothing about extras.
        builder.Property(oe => oe.Slug)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(oe => oe.UnitPrice)
            .IsRequired()
            .HasPrecision(18, 2);

        // Restrict (not the convention Cascade) so deleting an Extra that a historical — possibly
        // invoiced or receipted — order references is rejected at the database rather than silently
        // stripping the line. Order -> OrderExtra stays Cascade, as for OrderService and OrderPackage:
        // deleting an order still removes its lines; only the catalogue side is restricted.
        //
        // The bare .WithMany() is correct here, and the distinction matters. The recorded shadow-FK
        // landmine fires when the principal exposes a read-only .ToList() projection collection —
        // Service.IncludedInOrders is one, which is why OrderService binds its inverse by string. Extra
        // has no inverse collection at all, so this matches OrderPackage's bare form.
        builder.HasOne(oe => oe.Extra)
            .WithMany()
            .HasForeignKey(oe => oe.ExtraId)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per extra per order, enforced by the schema rather than by the caller's habit of
        // passing a distinct slug list.
        //
        // NO TenantId term, deliberately. OrderExtra derives BaseEntity, not Auditable, so it has no
        // TenantId to include — which is also why no global query filter attaches. Including a nullable
        // TenantId would make the index enforce nothing while that column is null.
        builder.HasIndex(oe => new { oe.OrderId, oe.ExtraId })
            .IsUnique()
            .HasDatabaseName("IX_OrderExtras_OrderId_ExtraId");
    }
}
