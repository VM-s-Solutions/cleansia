using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

// Inherits the shared TenantAuditable mapping so the stamped TenantId column is NOT NULL, varchar(26)
// and a foreign key like every other ITenantEntity table (ADR-0061 D8); a convention-only mapping left
// it text NULL.
public class OrderStatusTrackEntityConfiguration : TenantAuditableEntityConfiguration<OrderStatusTrack, string>
{
    public override void Configure(EntityTypeBuilder<OrderStatusTrack> builder)
    {
        base.Configure(builder);

        builder.Property(t => t.Sequence)
            .IsRequired();
    }
}
