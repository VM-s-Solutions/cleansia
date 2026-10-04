using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using FluentValidation.TestHelper;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A recurring occurrence is paid for once the customer confirms it, and confirming is where the terms in
/// force are asked for. The PaymentSheet intent is refused for an occurrence still awaiting that confirmation,
/// so the confirmation cannot be stepped around; once confirmed, a payment that failed or was abandoned is
/// retried here as before, and a one-off booking never waits on a confirmation at all.
/// </summary>
public sealed class CreatePaymentIntentAwaitingConfirmationTests
{
    private const string UserId = "user-occurrence-pay";
    private const string OrderId = "order-occurrence-pay";

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public CreatePaymentIntentAwaitingConfirmationTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
    }

    [Fact]
    public async Task An_Occurrence_The_Customer_Has_Not_Confirmed_Is_Refused()
    {
        Arrange(recurringTemplateId: "tmpl-weekly");

        var result = await Validator().TestValidateAsync(new CreatePaymentIntent.Command(OrderId));

        result.ShouldHaveValidationErrorFor(x => x.OrderId)
            .WithErrorMessage(BusinessErrorMessage.InvalidOrderStatusTransition);
    }

    [Fact]
    public async Task A_Confirmed_Occurrence_Is_Paid_Here()
    {
        var order = Arrange(recurringTemplateId: "tmpl-weekly");
        order.ConfirmByCustomer(DateTime.UtcNow);

        var result = await Validator().TestValidateAsync(new CreatePaymentIntent.Command(OrderId));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task A_One_Off_Booking_Is_Paid_Here()
    {
        Arrange(recurringTemplateId: null);

        var result = await Validator().TestValidateAsync(new CreatePaymentIntent.Command(OrderId));

        result.ShouldNotHaveAnyValidationErrors();
    }

    private Order Arrange(string? recurringTemplateId)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana@example.com",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Dlouhá 12", "Praha", "11000", "country-cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(48),
            paymentType: PaymentType.Card,
            totalPrice: 900m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Pending,
            userId: UserId,
            recurringTemplateId: recurringTemplateId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        _orders.Setup(r => r.GetByIdForOwnerAsync(OrderId, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    private CreatePaymentIntent.Validator Validator() => new(_orders.Object, _session.Object);
}
