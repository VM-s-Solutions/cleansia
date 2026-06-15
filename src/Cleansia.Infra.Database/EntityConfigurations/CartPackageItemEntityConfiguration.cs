using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CartPackageItemEntityConfiguration : BaseEntityConfiguration<CartPackageItem, string>
{
    public override void Configure(EntityTypeBuilder<CartPackageItem> builder)
    {
        base.Configure(builder);

        builder.Property(cartItem => cartItem.Quantity).IsRequired();

        builder.HasOne(cartItem => cartItem.Cart)
            .WithMany(cart => cart.PackageItems)
            .HasForeignKey(cartItem => cartItem.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict (not Cascade) so deleting a Package that sits in a live customer cart is rejected at
        // the database rather than silently orphaning the cart line; the admin in-use guard maps the
        // resulting restrict violation to package.in_use. Cart -> CartPackageItem stays Cascade.
        builder.HasOne(cartItem => cartItem.Package)
            .WithMany()
            .HasForeignKey(cartItem => cartItem.PackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}