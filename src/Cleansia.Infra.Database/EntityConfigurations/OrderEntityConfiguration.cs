using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Database.Converters;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class OrderEntityConfiguration : AuditableEntityConfiguration<Order, string>
{
    public override void Configure(EntityTypeBuilder<Order> builder)
    {
        base.Configure(builder);

        builder.Property(o => o.DisplayOrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.CustomerName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.CustomerEmail)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.CustomerPhone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(o => o.TotalPrice)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(o => o.Extras)
            .HasConversion(new JsonValueConverter<IReadOnlyDictionary<string, bool>>())
            .Metadata
            .SetValueComparer(new JsonValueComparer<IReadOnlyDictionary<string, bool>>());

        builder.Property(o => o.ConfirmationCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.StripeSessionId)
            .IsRequired()
            .HasMaxLength(100);
    }
}