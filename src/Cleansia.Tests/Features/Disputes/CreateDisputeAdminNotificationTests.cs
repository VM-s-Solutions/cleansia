using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// A filed dispute tells the ORDER's company once, through the one admin seam, with exactly the args
/// its catalogue entry declares — the order number, the reason as an enum name and the two ids the
/// console deep-links from — and never the customer's name, contact or the text they typed. A refused
/// filing tells nobody.
/// </summary>
public sealed class CreateDisputeAdminNotificationTests
{
    private const string CallerUserId = "caller-user-1";
    private const string OrderId = "order-1";
    private const string OrderTenantId = "company-of-the-order";
    private const string CustomerName = "Jana Nováková";
    private const string CustomerEmail = "jana.novakova@example.com";
    private const string CustomerPhone = "+420777123456";
    private const string Description = "The kitchen floor was not mopped and the bins were left full.";

    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly List<AdminEvent> _raised = [];

    public CreateDisputeAdminNotificationTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CallerUserId);
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute?)null);
        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((e, _) => _raised.Add(e))
            .Returns(Task.CompletedTask);
    }

    private CreateDispute.Handler CreateHandler() =>
        new(_disputeRepository.Object, Cleansia.Tests.Common.OrderAccessDoubles.Over(_orderRepository, _session),
            _session.Object, Mock.Of<ITenantProvider>(), new AuditContext(), _adminNotifier.Object);

    private Order ArrangeOrder(string? ownerUserId = CallerUserId, DateTime? cleaningDateTime = null)
    {
        var order = Order.Create(
            customerName: CustomerName,
            customerEmail: CustomerEmail,
            customerPhone: CustomerPhone,
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime ?? DateTime.UtcNow.AddHours(-6),
            paymentType: PaymentType.Card,
            totalPrice: 1250m,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Paid,
            userId: ownerUserId);
        order.Id = OrderId;
        order.TenantId = OrderTenantId;
        _orderRepository.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    private static CreateDispute.Command Command() => new(OrderId, DisputeReason.QualityIssue, Description);

    [Fact]
    public async Task A_Filed_Dispute_Raises_The_Event_Once_For_The_Orders_Company_With_The_Declared_Args()
    {
        var order = ArrangeOrder();

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var raised = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.DisputeFiled, raised.Key);
        Assert.Equal(OrderTenantId, raised.TenantId);
        Assert.Equal(result.Value.DisputeId, raised.Subject);

        var declared = AdminEventCatalog.Find(AdminNotificationEventCatalog.DisputeFiled).EmailArgOrder;
        Assert.Equal(declared.OrderBy(a => a), raised.Args.Keys.OrderBy(a => a));
        Assert.Equal(order.DisplayOrderNumber, raised.Args["orderNumber"]);
        Assert.Equal(nameof(DisputeReason.QualityIssue), raised.Args["reason"]);
        Assert.Equal(result.Value.DisputeId, raised.Args["disputeId"]);
        Assert.Equal(OrderId, raised.Args["orderId"]);
    }

    [Fact]
    public async Task No_Arg_Value_Is_The_Customer_Or_What_They_Typed()
    {
        ArrangeOrder();

        await CreateHandler().Handle(Command(), CancellationToken.None);

        var values = Assert.Single(_raised).Args.Values.ToList();
        Assert.All(values, value =>
        {
            Assert.DoesNotContain("Jana", value);
            Assert.DoesNotContain("Nováková", value);
            Assert.DoesNotContain("@", value);
            Assert.DoesNotContain("+420", value);
            Assert.DoesNotContain("kitchen", value);
        });
        Assert.True(Enum.TryParse<DisputeReason>(Assert.Single(_raised).Args["reason"], ignoreCase: false, out _));
    }

    [Fact]
    public async Task A_Filing_Before_The_Clean_Tells_Nobody()
    {
        ArrangeOrder(cleaningDateTime: DateTime.UtcNow.AddDays(1));

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_raised);
    }

    [Fact]
    public async Task A_Filing_On_Another_Customers_Order_Tells_Nobody()
    {
        ArrangeOrder(ownerUserId: "someone-else");

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_raised);
    }

    [Fact]
    public async Task A_Second_Filing_On_An_Open_Dispute_Tells_Nobody()
    {
        ArrangeOrder();
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dispute(OrderId, CallerUserId, DisputeReason.QualityIssue, Description, CallerUserId));

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_raised);
    }
}
