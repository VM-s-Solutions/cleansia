using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Domain.Payments;

/// <summary>
/// The price a customer did not pay the cleaner at the door is what the cleaner would have collected: the
/// sale less whatever the customer's credit already settled. Only an account can owe it.
/// </summary>
public sealed class ReceivableForUnpaidCashTests
{
    [Fact]
    public void It_Is_Open_For_The_Price_Less_The_Credit_Applied_In_The_Orders_Currency()
    {
        var order = CashOrder(userId: "user-door");
        order.ApplyCredit(150m, "user-door");

        var receivable = Receivable.ForUnpaidCash(order);

        Assert.Equal(
            (ReceivableKind.UnpaidCash, ReceivableStatus.Open, 1350m, order.Id, "user-door", order.CurrencyId, 0),
            (receivable.Kind, receivable.Status, receivable.Amount, receivable.OrderId, receivable.UserId, receivable.CurrencyId, receivable.Attempts));
    }

    [Fact]
    public void A_Guest_Order_Cannot_Owe_It()
    {
        Assert.Throws<InvalidOperationException>(() => Receivable.ForUnpaidCash(CashOrder(userId: null)));
    }

    private static Order CashOrder(string? userId) => Order.Create(
        customerName: "Door Customer",
        customerEmail: "door@example.test",
        customerPhone: "+420777000111",
        customerAddress: Address.Create("Dverni 1", "Praha", "11000", "cz"),
        rooms: 2,
        bathrooms: 1,
        cleaningDateTime: DateTime.UtcNow.AddHours(-2),
        paymentType: PaymentType.Cash,
        totalPrice: 1500m,
        currencyId: "currency-czk",
        paymentStatus: PaymentStatus.Pending,
        userId: userId,
        cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
}
