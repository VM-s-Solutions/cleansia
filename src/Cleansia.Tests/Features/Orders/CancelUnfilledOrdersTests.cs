using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The sweep that finds a booking whose time arrived with nobody on it.
///
/// <para><b>This is the only automatic money-mover in the platform</b>, so the tests that matter here
/// are the ones about restraint: what it must NOT pay, and what it must not pay TWICE. The happy path
/// is one assertion; the guards are the rest.</para>
///
/// <para>The justification for moving money with no human in the loop is evidentiary and narrow: an
/// empty seat at the appointed time is a fact on the platform's own record, unlike every other version
/// of "the cleaner did not arrive", which rests on a tap that may simply have been forgotten. If these
/// tests ever have to be relaxed to admit a case where somebody WAS assigned, that reasoning has gone
/// and the automatic refund should go with it.</para>
/// </summary>
public class CancelUnfilledOrdersTests
{
    private const string DefaultCurrencyId = "czk";
    private const string ForeignCurrencyId = "eur";
    private const string UserId = "user-unfilled-1";

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<IRefundService> _refunds = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<ITenantProvider> _tenants = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private CreditAccount? _account;

    public CancelUnfilledOrdersTests()
    {
        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1m);
        currency.Id = DefaultCurrencyId;
        _currencies.Setup(c => c.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        _refunds.Setup(r => r.IssueRefundAsync(
                It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(
                new RefundResult("refund-1", "refund:key", 1000m, RefundStatus.Succeeded, false)));

        _credit.Setup(c => c.EnsureForUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, string currencyId, CancellationToken _) =>
                _account ??= CreditAccount.Create(userId, currencyId, "system"));
    }

    private Order UnfilledOrder(
        string orderId = "order-unfilled-1",
        OrderStatus status = OrderStatus.Confirmed,
        PaymentType paymentType = PaymentType.Card,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        string? userId = UserId,
        string currencyId = DefaultCurrencyId)
    {
        return BuildOrder(orderId, status, paymentType, paymentStatus, userId, currencyId,
            cleaningDateTime: DateTime.UtcNow.AddHours(-2));
    }

    /// <summary>
    /// Built here rather than through ValidatorTestHelpers, which pins the currency to CZK and takes no
    /// user id — the two things these tests vary.
    /// </summary>
    private static Order BuildOrder(
        string orderId,
        OrderStatus status,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        string? userId,
        string currencyId,
        DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "test@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: currencyId,
            paymentStatus: paymentStatus,
            userId: userId);

        order.Id = orderId;
        order.SetMaxEmployees(1);

        // A card order is only refundable on a real Stripe surface. Without this the sweep correctly
        // skips the refund and the money assertions below would pass for the wrong reason.
        if (paymentType == PaymentType.Card)
        {
            order.AssignStripePaymentIntentId($"pi_test_{orderId}");
        }

        var now = DateTimeOffset.UtcNow;
        var initial = OrderStatusTrack.Create(OrderStatus.New, order);
        initial.Created("test", now.AddMinutes(-10));
        order.AddOrderStatus(initial);

        if (status != OrderStatus.New)
        {
            var latest = OrderStatusTrack.Create(status, order);
            latest.Created("test", now);
            order.AddOrderStatus(latest);
        }

        return order;
    }

    private void Arrange(params Order[] orders) =>
        _orders.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(orders.AsQueryable().BuildMock());

    private CancelUnfilledOrders.Handler Handler() =>
        new(_orders.Object, _credit.Object, _currencies.Object, _refunds.Object,
            _notifications.Object, _tenants.Object, _uow.Object,
            NullLogger<CancelUnfilledOrders.Handler>.Instance);

    private Task<BusinessResult<CancelUnfilledOrders.Response>> Sweep() =>
        Handler().Handle(new CancelUnfilledOrders.Command(), default);

    [Fact]
    public async Task AnUnfilledPaidOrderIsCancelledRefundedAndCredited()
    {
        var order = UnfilledOrder();
        Arrange(order);

        var result = await Sweep();

        Assert.Equal(1, result.Value.CancelledCount);
        Assert.Equal(1, result.Value.RefundedCount);
        Assert.Equal(1, result.Value.CreditedCount);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(OrderCancellationReasons.NoCleanerAvailable, order.CancellationReason);
        Assert.Equal(CancelledBy.System, order.CancelledBy);
        // Platform fault: the customer pays no cancellation fee.
        Assert.Equal(order.TotalPrice, order.CancellationRefundAmount);
        Assert.Equal(BookingPolicy.NoShowCreditCzk, _account!.Balance);
    }

    /// <summary>
    /// A cleaner may take a job AFTER its start time — TakeOrder has no lead-time gate — so a seat
    /// filled between the read and the write must not have the booking cancelled out from under it.
    /// </summary>
    [Fact]
    public async Task AnOrderWithACrewIsLeftAlone()
    {
        var order = UnfilledOrder();
        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee("emp-1", ContractStatus.Approved)));
        Arrange(order);

        var result = await Sweep();

        Assert.Equal(0, result.Value.CancelledCount);
        Assert.NotEqual(OrderStatus.Cancelled, order.CurrentStatus);
        _refunds.Verify(
            r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// A job somebody STARTED is not a job nobody turned up to. Refunding it in full because the crew
    /// count reached zero would pay back a clean that was partly done — which is why the sweep uses its
    /// own NeverStarted set rather than the widened OfferableStatuses.
    /// </summary>
    [Theory]
    [InlineData(OrderStatus.OnTheWay)]
    [InlineData(OrderStatus.InProgress)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task AStartedOrFinishedOrderIsNeverSwept(OrderStatus status)
    {
        Arrange(UnfilledOrder(status: status));

        var result = await Sweep();

        Assert.Equal(0, result.Value.CancelledCount);
        _refunds.Verify(
            r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// THE COLD-START BOUND. Every row is unswept on the first tick, so without the lookback floor the
    /// first production run would refund the platform's entire history of unfilled orders in one pass,
    /// unattended.
    /// </summary>
    [Fact]
    public async Task OrdersOlderThanTheLookbackAreOutOfReach()
    {
        Arrange(BuildOrder(
            "order-ancient", OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Paid,
            UserId, DefaultCurrencyId, cleaningDateTime: DateTime.UtcNow.AddDays(-30)));

        var result = await Sweep();

        Assert.Equal(0, result.Value.CancelledCount);
    }

    /// <summary>
    /// A guest has nowhere to put credit: Order.UserId is nullable and CreditAccount.UserId is not,
    /// behind an FK to Users. They still get the whole refund — which is exactly what the home page
    /// now promises in five locales.
    /// </summary>
    [Fact]
    public async Task AGuestIsRefundedButNotCredited()
    {
        Arrange(UnfilledOrder(userId: null));

        var result = await Sweep();

        Assert.Equal(1, result.Value.CancelledCount);
        Assert.Equal(1, result.Value.RefundedCount);
        Assert.Equal(0, result.Value.CreditedCount);
        _credit.Verify(
            c => c.EnsureForUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// NoShowCreditCzk is 250 CZK and a credit account keeps whatever currency it was opened in,
    /// converting nothing — so on a EUR order this would hand over 250 EUR, roughly twenty-five times
    /// the intended apology. Fails closed: no credit, full refund, and a log line.
    /// </summary>
    [Fact]
    public async Task ANonDefaultCurrencyOrderIsRefundedButNotCredited()
    {
        Arrange(UnfilledOrder(currencyId: ForeignCurrencyId));

        var result = await Sweep();

        Assert.Equal(1, result.Value.CancelledCount);
        Assert.Equal(1, result.Value.RefundedCount);
        Assert.Equal(0, result.Value.CreditedCount);
    }

    /// <summary>
    /// One key per ORDER, so a retried tick or a re-entry after a failed commit pays the apology once.
    /// The ledger's IdempotencyKey carries a plain unique index that collapses the second write.
    /// </summary>
    [Fact]
    public async Task TheApologyCreditIsKeyedOncePerOrder()
    {
        var order = UnfilledOrder();
        Arrange(order);

        await Sweep();

        var row = Assert.Single(_account!.Transactions);
        Assert.Equal($"cleaner-noshow:{order.Id}", row.IdempotencyKey);
        Assert.Equal(CreditTransactionReason.CleanerNoShow, row.Reason);
    }

    /// <summary>
    /// A cash booking was never charged, so there is nothing to refund — but the platform still failed
    /// them, so the apology stands.
    /// </summary>
    [Fact]
    public async Task ACashOrderIsCancelledAndCreditedWithNoRefund()
    {
        Arrange(UnfilledOrder(
            paymentType: PaymentType.Cash, paymentStatus: PaymentStatus.Pending));

        var result = await Sweep();

        Assert.Equal(1, result.Value.CancelledCount);
        Assert.Equal(0, result.Value.RefundedCount);
        Assert.Equal(1, result.Value.CreditedCount);
    }

    /// <summary>
    /// The customer must be told, and told about the money. Owner ruling 2026-09-06: the 250 is
    /// announced explicitly rather than left for them to find in the order detail.
    ///
    /// <para>One message, keyed on the order — a cancellation happens once per order, so the bare id
    /// is a safe subject here, unlike a refund.</para>
    /// </summary>
    [Fact]
    public async Task TheCustomerIsToldAboutTheMoney()
    {
        var order = UnfilledOrder();
        Arrange(order);

        await Sweep();

        _notifications.Verify(n => n.NotifyAsync(
            UserId,
            NotificationEventCatalog.OrderNoCleanerRefunded,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            order.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// THE HONESTY GUARD. The announcing key promises credit, so it is sent only when credit was
    /// actually issued. A non-default currency is refused the grant and a guest has nowhere to hold
    /// it — both get the plain cancellation instead. Promising money nobody received would be worse
    /// than saying less.
    /// </summary>
    [Fact]
    public async Task ACustomerWhoGotNoCreditIsNotPromisedAny()
    {
        var order = UnfilledOrder(currencyId: ForeignCurrencyId);
        Arrange(order);

        await Sweep();

        _notifications.Verify(n => n.NotifyAsync(
            UserId,
            NotificationEventCatalog.OrderCancelled,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            order.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.NotifyAsync(
            It.IsAny<string>(),
            NotificationEventCatalog.OrderNoCleanerRefunded,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The commit is INSIDE the tenant loop. Rows are stamped from the ambient tenant at commit time,
    /// so one deferred commit would stamp every group with whichever tenant was processed last.
    /// </summary>
    [Fact]
    public async Task EachTenantGroupCommitsSeparately()
    {
        var first = UnfilledOrder("order-t1");
        var second = UnfilledOrder("order-t2");
        first.TenantId = "tenant-a";
        second.TenantId = "tenant-b";
        Arrange(first, second);

        await Sweep();

        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _tenants.Verify(t => t.SetTenantOverride("tenant-a"), Times.Once);
        _tenants.Verify(t => t.SetTenantOverride("tenant-b"), Times.Once);
    }
}
