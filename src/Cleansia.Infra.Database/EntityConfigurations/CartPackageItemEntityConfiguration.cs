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

        builder.HasOne(cartItem => cartItem.Package)
            .WithMany()
            .HasForeignKey(cartItem => cartItem.PackageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}