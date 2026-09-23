using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Domain.Orders;

/// <summary>
/// An address row can gain another owner concurrently, so the source is never blanked in place.
/// </summary>
public sealed class OrderAnonymizeCustomerAddressTests
{
    private const string Street = "Sdilena 5";

    [Fact]
    public void The_Source_Is_Left_Alone_And_The_Order_Moves_To_An_Anonymised_Copy_Under_Its_Own_Company()
    {
        var shared = Address.Create(Street, "Praha", "11000", "country-cz", "Stredocesky", 50.07, 14.43);
        shared.TenantId = "tenant-address";
        var order = NewOrder(shared);
        order.TenantId = "tenant-order";

        var copy = order.AnonymizeCustomerAddress();

        Assert.NotNull(copy);
        Assert.Same(copy, order.CustomerAddress);
        Assert.Equal(copy.Id, order.CustomerAddressId);
        Assert.NotEqual(shared.Id, copy.Id);
        Assert.Equal(AnonymizationMarker.Value, copy.Street);
        Assert.Equal(AnonymizationMarker.Value, copy.City);
        Assert.Equal(AnonymizationMarker.Value, copy.ZipCode);
        Assert.Equal(AnonymizationMarker.Value, copy.State);
        Assert.Null(copy.Latitude);
        Assert.Null(copy.Longitude);
        Assert.Equal("country-cz", copy.CountryId);
        Assert.Equal("tenant-order", copy.TenantId);

        Assert.Equal(Street, shared.Street);
        Assert.Equal("Stredocesky", shared.State);
        Assert.Equal(50.07, shared.Latitude);
    }

    private static Order NewOrder(Address address) =>
        Order.Create(
            customerName: "Jana Novakova",
            customerEmail: "jana@example.test",
            customerPhone: "+420777123456",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-30),
            paymentType: PaymentType.Card,
            totalPrice: 1250m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid);
}
