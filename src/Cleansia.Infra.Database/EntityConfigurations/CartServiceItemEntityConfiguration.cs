using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CartServiceItemEntityConfiguration : BaseEntityConfiguration<CartServiceItem, string>
{
    public override void Configure(EntityTypeBuilder<CartServiceItem> builder)
    {
        base.Configure(builder);

        builder.Property(cartItem => cartItem.Quantity).IsRequired();

        builder.HasOne(cartItem => cartItem.Cart)
            .WithMany(cart => cart.ServiceItems)
            .HasForeignKey(cartItem => cartItem.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict (not Cascade) so deleting a Service that sits in a live customer cart is rejected at
        // the database rather than silently orphaning the cart line; the admin in-use guard maps the
        // resulting restrict violation to service.in_use. Cart -> CartServiceItem stays Cascade.
        builder.HasOne(cartItem => cartItem.Service)
            .WithMany()
            .HasForeignKey(cartItem => cartItem.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}