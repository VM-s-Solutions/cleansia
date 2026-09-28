using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Domain.Orders;

/// <summary>
/// The anonymised order keeps the sale and loses the person. Floor, flat, how to get in and the customer's
/// own words about why they cancelled are the person too. A reason code the platform wrote is not — and the
/// wind-down's refund re-drive still finds its orders by it.
/// </summary>
public sealed class OrderAnonymizeCustomerDataTests
{
    [Fact]
    public void Floor_Flat_Access_Mode_And_A_Customers_Cancellation_Reason_Are_Cleared()
    {
        var order = NewOrder();
        order.Cancel(DateTime.UtcNow, CancelledBy.Customer, 0m, 0m, "My ex-husband lives at this flat now");

        order.AnonymizeCustomerData();

        Assert.Equal(AnonymizationMarker.Value, order.CustomerName);
        Assert.Null(order.CustomerFloor);
        Assert.Null(order.CustomerApartment);
        Assert.Null(order.AccessMode);
        Assert.Null(order.CancellationReason);
    }

    [Fact]
    public void An_Admins_Free_Text_Reason_Is_Cleared()
    {
        var order = NewOrder();
        order.Cancel(DateTime.UtcNow, CancelledBy.Admin, 0m, 0m, "Customer called, says Mr Novak is ill");

        order.AnonymizeCustomerData();

        Assert.Null(order.CancellationReason);
    }

    [Fact]
    public void A_Platform_Reason_Code_Survives()
    {
        var order = NewOrder();
        order.Cancel(DateTime.UtcNow, CancelledBy.System, 0m, 0m, OrderCancellationReasons.CompanyWindDown);

        order.AnonymizeCustomerData();

        Assert.Equal(OrderCancellationReasons.CompanyWindDown, order.CancellationReason);
    }

    private static Order NewOrder() =>
        Order.Create(
            customerName: "Milada Novotna",
            customerEmail: "milada@example.com",
            customerPhone: "+420777111222",
            customerAddress: Address.Create("Dlouha 14", "Praha", "11000", "country-cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            customerFloor: "3",
            customerApartment: "12B",
            accessMode: "door_code");
}
