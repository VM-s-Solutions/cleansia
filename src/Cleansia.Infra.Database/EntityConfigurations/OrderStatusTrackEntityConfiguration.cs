using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

// Pins only the new Sequence column (NOT NULL). Everything else on OrderStatusTrack stays
// convention-mapped exactly as before, so the Initial-regen diff is the single added column.
public class OrderStatusTrackEntityConfiguration : IEntityTypeConfiguration<OrderStatusTrack>
{
    public void Configure(EntityTypeBuilder<OrderStatusTrack> builder)
    {
        builder.Property(t => t.Sequence)
            .IsRequired();
    }
}
