using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
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
/// A recurring occurrence is never offerable at creation; the customer's confirm is what makes the
/// cash flavour so, and that is where the order's company is told — once, with the declared args.
/// The card flavour confirms nothing here (the webhook does), so it tells nobody, and a confirm the
/// handler refuses tells nobody either.
/// </summary>
public sealed class ConfirmRecurringOrderTellsAdministratorsTests
{
    private const string OrderId = "order-recurring-tells-1";
    private const string CustomerUserId = "user-customer-recurring-tells";
    private const string TenantId = "company-of-the-order";
    private const string CountryId = "country-cz-recurring-tells";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly List<AdminEvent> _raised = [];

    public ConfirmRecurringOrderTellsAdministratorsTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CustomerUserId);
        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((e, _) => _raised.Add(e))
            .Returns(Task.CompletedTask);
    }

    private ConfirmRecurringOrder.Handler Handler() => new(
        OrderAccessDoubles.Over(_orderRepository, _session),
        new Mock<ICreditAccountRepository>().Object,
        new Mock<IUserRepository>().Object,
        _session.Object,
        Mock.Of<ITenantProvider>(),
        new Mock<IStripeClient>().Object,
        new StripeConfig(new ConfigurationBuilder().Build()),
        new Mock<IPendingDispatch>().Object,
        new Mock<INotificationProducer>().Object,
        NoPreferredCleanerHold.Resolver,
        _adminNotifier.Object,
        new AuditContext(),
        NullLogger<ConfirmRecurringOrder.Handler>.Instance);

    private Order ArrangeOrder(PaymentType paymentType, PaymentStatus paymentStatus = PaymentStatus.Pending)
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana.novakova@example.com",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: paymentType,
            totalPrice: 990m,
            currencyId: "currency-czk",
            paymentStatus: paymentStatus,
            userId: CustomerUserId,
            recurringTemplateId: "tmpl-weekly-tells");
        order.Id = OrderId;
        order.TenantId = TenantId;
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech koruna"));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        _orderRepository.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    [Fact]
    public async Task The_Cash_Confirm_Tells_The_Orders_Company_Once_With_The_Declared_Args()
    {
        var order = ArrangeOrder(PaymentType.Cash);

        var result = await Handler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var raised = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.OrderNew, raised.Key);
        Assert.Equal(TenantId, raised.TenantId);
        Assert.Equal(OrderId, raised.Subject);
        var declared = AdminEventCatalog.Find(AdminNotificationEventCatalog.OrderNew).EmailArgOrder;
        Assert.Equal(declared.OrderBy(a => a), raised.Args.Keys.OrderBy(a => a));
        Assert.Equal(order.DisplayOrderNumber, raised.Args["orderNumber"]);
        Assert.Equal("990 Kč", raised.Args["amount"]);
        Assert.Equal(nameof(PaymentType.Cash), raised.Args["paymentType"]);
        Assert.Equal(CountryId, raised.Args["countryId"]);
        Assert.Equal(OrderId, raised.Args["orderId"]);
    }

    [Fact]
    public async Task A_Second_Confirm_Of_The_Same_Occurrence_Is_Refused_And_Tells_Nobody()
    {
        ArrangeOrder(PaymentType.Cash);
        var handler = Handler();

        await handler.Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);
        var replay = await handler.Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(replay.IsFailure);
        Assert.Single(_raised);
    }

    [Fact]
    public async Task The_Card_Confirm_Tells_Nobody_Because_The_Webhook_Will()
    {
        ArrangeOrder(PaymentType.Card);

        await Handler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.Empty(_raised);
    }
}
