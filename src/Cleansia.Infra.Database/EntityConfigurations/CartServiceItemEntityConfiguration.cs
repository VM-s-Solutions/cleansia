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

        builder.HasOne(cartItem => cartItem.Service)
            .WithMany()
            .HasForeignKey(cartItem => cartItem.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}