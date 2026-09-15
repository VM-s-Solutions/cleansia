using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

// Inherits the shared Auditable mapping so the stamped TenantId column is NOT NULL and varchar(26)
// like every other ITenantEntity table (ADR-0061 D8); a convention-only mapping left it text NULL.
public class OrderStatusTrackEntityConfiguration : TenantAuditableEntityConfiguration<OrderStatusTrack, string>
{
    public override void Configure(EntityTypeBuilder<OrderStatusTrack> builder)
    {
        base.Configure(builder);

        builder.Property(t => t.Sequence)
            .IsRequired();
    }
}
