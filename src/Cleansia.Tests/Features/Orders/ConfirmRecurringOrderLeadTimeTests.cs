using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A recurring occurrence is confirmed on the same two-hour floor a one-off booking is created on: closer
/// than that, no cleaner can be relied on to take it, and a card confirm would charge for a slot that
/// the auto-cancel sweep is about to retract.
/// </summary>
public sealed class ConfirmRecurringOrderLeadTimeTests
{
    private const string OrderId = "order-recurring-lead-time";
    private const string CustomerUserId = "user-recurring-lead-time";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly AuditContext _audit = new();

    public ConfirmRecurringOrderLeadTimeTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CustomerUserId);
    }

    [Fact]
    public async Task An_Occurrence_Under_Two_Hours_Away_Is_Refused_And_Nothing_Is_Stamped()
    {
        var order = ArrangeOccurrence(DateTime.UtcNow.AddMinutes(90));

        var result = await Handler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.CleaningDateBelowLeadTime, result.Error!.Message);
        Assert.Null(order.CustomerConfirmedAt);
        _pending.VerifyNoOtherCalls();
        _stripe.VerifyNoOtherCalls();
        Assert.Null(_audit.DrainSnapshot());
    }

    [Fact]
    public async Task An_Occurrence_Past_The_Floor_Confirms()
    {
        var order = ArrangeOccurrence(DateTime.UtcNow.AddHours(3));

        var result = await Handler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(order.CustomerConfirmedAt);
    }

    private ConfirmRecurringOrder.Handler Handler() => new(
        OrderAccessDoubles.Over(_orderRepository, _session),
        Mock.Of<ICreditAccountRepository>(),
        Mock.Of<IUserRepository>(),
        _session.Object,
        Mock.Of<ITenantProvider>(),
        _stripe.Object,
        new StripeConfig(new ConfigurationBuilder().Build()),
        new OrderChannelProvider(OrderChannel.Mobile),
        _pending.Object,
        Mock.Of<INotificationProducer>(),
        NoPreferredCleanerHold.Resolver,
        Mock.Of<IAdminNotifier>(),
        _audit,
        NullLogger<ConfirmRecurringOrder.Handler>.Instance);

    private Order ArrangeOccurrence(DateTime cleaningUtc)
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana.novakova@example.com",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", "country-cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningUtc,
            paymentType: PaymentType.Cash,
            totalPrice: 990m,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Pending,
            userId: CustomerUserId,
            recurringTemplateId: "tmpl-weekly-lead-time");
        order.Id = OrderId;
        order.TenantId = "company-of-the-order";
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech koruna"));
        order.UpdateEstimatedTime(120);
        order.CalculateRequiredEmployees(BookingPolicy.SpareSeatsPerOrder);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        _orderRepository.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }
}
