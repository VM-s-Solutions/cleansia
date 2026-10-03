using Cleansia.Core.AppServices.Common;
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
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging;
using MockQueryable;
using Moq;
using Polly.CircuitBreaker;
using Polly.Timeout;

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
    private const decimal CzkApology = 250m;

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Mock<IRefundService> _refunds = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<IGuestOrderAccessTokenRepository> _guestTokens = new();
    private readonly List<(string Queue, string Key, object Message)> _enqueued = [];
    private readonly Mock<ITenantProvider> _tenants = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly List<(LogLevel Level, string Message)> _log = [];

    /// <summary>Seeded like the DEV CZK row: an authored apology figure.</summary>
    private readonly Currency _czk;

    /// <summary>Seeded like the DEV EUR row: no apology figure authored yet.</summary>
    private readonly Currency _eur;

    private readonly Dictionary<string, CreditAccount> _accounts = [];

    private CreditAccount? _account => _accounts.GetValueOrDefault(DefaultCurrencyId);

    public CancelUnfilledOrdersTests()
    {
        _czk = Currency.Create("CZK", "Kč", "Czech koruna");
        _czk.Id = DefaultCurrencyId;
        _czk.SetNoShowCredit(CzkApology);

        _eur = Currency.Create("EUR", "€", "Euro");
        _eur.Id = ForeignCurrencyId;

        _refunds.Setup(r => r.IssueRefundAsync(
                It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(
                new RefundResult("refund-1", "refund:key", 1000m, RefundStatus.Succeeded, false)));

        _guestTokens.Setup(r => r.GetLiveForOrderIgnoringTenantAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GuestOrderAccessToken>());

        _credit.Setup(c => c.EnsureForUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, string currencyId, CancellationToken _) =>
            {
                if (!_accounts.TryGetValue(currencyId, out var account))
                {
                    account = CreditAccount.Create(userId, currencyId, "system");
                    _accounts[currencyId] = account;
                }

                return account;
            });
    }

    private Order UnfilledOrder(
        string orderId = "order-unfilled-1",
        OrderStatus status = OrderStatus.Confirmed,
        PaymentType paymentType = PaymentType.Card,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        string? userId = UserId,
        Currency? currency = null)
    {
        return BuildOrder(orderId, status, paymentType, paymentStatus, userId, currency ?? _czk,
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
        Currency currency,
        DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "test@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime,
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: userId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);

        order.Id = orderId;
        // The sweep reads the apology figure off the order's currency navigation (Include(o => o.Currency)).
        order.SetCurrency(currency);
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
        new(_orders.Object,
            new CleanerNoShowCancellation(_credit.Object, _refunds.Object, _notifications.Object,
                new GuestOrderAccessTokenIssuer(_guestTokens.Object), new RecordingDispatch(_enqueued),
                new CapturingLogger<CleanerNoShowCancellation>(_log)),
            _tenants.Object, _uow.Object,
            new CapturingLogger<CancelUnfilledOrders.Handler>(_log));

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
        Assert.Equal(CzkApology, _account!.Balance);
    }

    /// <summary>
    /// A paid order whose only cleaner dropped it went back to New through the domain writer. At its
    /// slot it is exactly a never-taken one — the same empty seat at the same instant — and the sweep
    /// pays it exactly the same, with no second money path.
    /// </summary>
    [Fact]
    public async Task AnOrderDroppedBackToNewIsSweptExactlyLikeANeverTakenOne()
    {
        var order = UnfilledOrder();
        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee("emp-left", ContractStatus.Approved)));
        order.UnassignEmployee("emp-left");
        Assert.True(order.ReturnToBoardIfUnstaffed());
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Arrange(order);

        var result = await Sweep();

        Assert.Equal(1, result.Value.CancelledCount);
        Assert.Equal(1, result.Value.RefundedCount);
        Assert.Equal(1, result.Value.CreditedCount);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(OrderCancellationReasons.NoCleanerAvailable, order.CancellationReason);
        Assert.Equal(order.TotalPrice, order.CancellationRefundAmount);
        Assert.Equal(CzkApology, _account!.Balance);
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
            UserId, _czk, cleaningDateTime: DateTime.UtcNow.AddDays(-30)));

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

    [Fact]
    public async Task AnErasedAccount_IsRefundedWithoutApologyCreditOrAPromiseOfCredit()
    {
        var order = UnfilledOrder();
        Arrange(order);
        // The sweep may have loaded the order before erasure; the locked account lookup is final.
        _credit.Setup(c => c.EnsureForUserAsync(UserId, DefaultCurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreditAccount?)null);

        var result = await Sweep();

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.CancelledCount);
        Assert.Equal(1, result.Value.RefundedCount);
        Assert.Equal(0, result.Value.CreditedCount);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Empty(_accounts);
        _credit.Verify(c => c.EnsureForUserAsync(UserId, DefaultCurrencyId, It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.NotifyAsync(
            UserId, NotificationEventCatalog.OrderCancelled,
            It.Is<Dictionary<string, string>>(args => !args.ContainsKey("amount")),
            It.IsAny<string?>(), order.Id, It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.NotifyAsync(
            It.IsAny<string>(), NotificationEventCatalog.OrderNoCleanerRefunded,
            It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The apology figure is authored PER CURRENCY, and a credit account keeps whatever currency it
    /// was opened in, converting nothing — so a currency with no authored figure pays none rather than
    /// borrowing another currency's number. Fails closed: no credit, full refund, and a warning that
    /// names the currency.
    /// </summary>
    [Fact]
    public async Task AnOrderInACurrencyWithNoAuthoredCreditIsRefundedButNotCredited()
    {
        Arrange(UnfilledOrder(currency: _eur));

        var result = await Sweep();

        Assert.Equal(1, result.Value.CancelledCount);
        Assert.Equal(1, result.Value.RefundedCount);
        Assert.Equal(0, result.Value.CreditedCount);
        Assert.Empty(_accounts);
        var warning = Assert.Single(_log, e => e.Level == LogLevel.Warning);
        Assert.Contains("EUR", warning.Message);
    }

    /// <summary>
    /// The day an admin authors a EUR figure, a EUR order is credited that figure into the customer's
    /// EUR account — never the CZK number, never into the CZK account.
    /// </summary>
    [Fact]
    public async Task AnOrderInACurrencyWithAnAuthoredCreditIsCreditedInThatCurrency()
    {
        _eur.SetNoShowCredit(10m);
        Arrange(UnfilledOrder(currency: _eur));

        var result = await Sweep();

        Assert.Equal(1, result.Value.CreditedCount);
        var account = Assert.Single(_accounts).Value;
        Assert.Equal(ForeignCurrencyId, account.CurrencyId);
        Assert.Equal(10m, account.Balance);
        _credit.Verify(
            c => c.EnsureForUserAsync(UserId, ForeignCurrencyId, It.IsAny<CancellationToken>()),
            Times.Once);
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
    /// Owner ruling 2026-09-28: a recurring cash occurrence the customer confirmed stays unpaid until the
    /// cleaner records the cash, and it was offered on the board, so at its slot with nobody on it it is
    /// swept like any cash booking. One the customer never confirmed was never offered, and is not.
    /// </summary>
    [Fact]
    public async Task A_Confirmed_Recurring_Cash_Occurrence_Is_Swept_And_An_Unconfirmed_One_Is_Not()
    {
        var confirmed = RecurringCashOccurrence("order-recurring-confirmed", "tmpl-confirmed");
        confirmed.ConfirmByCustomer(DateTime.UtcNow.AddDays(-1));
        var unconfirmed = RecurringCashOccurrence("order-recurring-unconfirmed", "tmpl-unconfirmed");
        Arrange(confirmed, unconfirmed);

        var result = await Sweep();

        Assert.Equal(1, result.Value.CancelledCount);
        Assert.Equal(OrderStatus.Cancelled, confirmed.CurrentStatus);
        Assert.Equal(OrderStatus.New, unconfirmed.CurrentStatus);
    }

    private Order RecurringCashOccurrence(string orderId, string recurringTemplateId)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "test@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(-2),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: _czk.Id,
            paymentStatus: PaymentStatus.Pending,
            userId: UserId,
            recurringTemplateId: recurringTemplateId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = orderId;
        order.SetCurrency(_czk);
        order.SetMaxEmployees(1);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }

    /// <summary>
    /// A card refund of the whole sale already puts the credit share back on its own leg, so the
    /// order-ended return must not pay the same credit a second time.
    /// </summary>
    [Fact]
    public async Task ARefundedCardOrdersAppliedCreditIsNotReturnedASecondTime()
    {
        var order = UnfilledOrder();
        order.ApplyCredit(300m, UserId);
        Arrange(order);

        var result = await Sweep();

        Assert.Equal(1, result.Value.RefundedCount);
        _refunds.Verify(r => r.IssueRefundAsync(
            It.Is<RefundRequest>(q => q.OrderId == order.Id && q.Amount == order.TotalPrice),
            It.IsAny<CancellationToken>()), Times.Once);
        VerifyOrderEndedCreditReturn(order, Times.Never());
    }

    /// <summary>
    /// The card refund that failed waits for the hourly re-drive, but the credit comes back now. The
    /// re-drive nets off what already went back, so this cannot double it.
    /// </summary>
    [Fact]
    public async Task AFailedCardRefundStillReturnsTheAppliedCreditOnce()
    {
        var order = UnfilledOrder();
        order.ApplyCredit(300m, UserId);
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundFailed)));
        Arrange(order);

        var result = await Sweep();

        Assert.Equal(0, result.Value.RefundedCount);
        VerifyOrderEndedCreditReturn(order, Times.Once());
    }

    /// <summary>
    /// RefundService turns a Stripe refusal into a Failure but lets a transport fault escape, after its
    /// claim commit has already saved the cancel. Rethrown, the order would leave the sweep for good
    /// with its credit never returned.
    /// </summary>
    [Theory]
    [MemberData(nameof(StripeTransportFailures))]
    public async Task AStripeTransportFaultIsAFailedRefundAndTheCreditStillComesBackOnce(Exception failure)
    {
        var order = UnfilledOrder();
        order.ApplyCredit(300m, UserId);
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        Arrange(order);

        var result = await Sweep();

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.CancelledCount);
        Assert.Equal(0, result.Value.RefundedCount);
        Assert.Equal(1, result.Value.CreditedCount);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        VerifyOrderEndedCreditReturn(order, Times.Once());
        Assert.Equal(CzkApology, _account!.Balance);
        _notifications.Verify(n => n.NotifyAsync(
            UserId, It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), order.Id, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(_log, e => e.Level == LogLevel.Error && e.Message.Contains(order.Id));
    }

    public static TheoryData<Exception> StripeTransportFailures() =>
    [
        new HttpRequestException("connection reset"),
        new TimeoutException("gateway timeout"),
        new TaskCanceledException("the request timed out"),
        new TimeoutRejectedException("total request timeout"),
        new BrokenCircuitException("circuit open"),
    ];

    /// <summary>A caller-requested cancellation is a genuine abort, not a Stripe outage.</summary>
    [Fact]
    public async Task ACancelledSweepIsNotLaunderedIntoAFailedRefund()
    {
        var order = UnfilledOrder();
        order.ApplyCredit(300m, UserId);
        using var cancellation = new CancellationTokenSource();
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await cancellation.CancelAsync();
                throw new TaskCanceledException();
            });
        Arrange(order);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => Handler().Handle(new CancelUnfilledOrders.Command(), cancellation.Token));

        VerifyOrderEndedCreditReturn(order, Times.Never());
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnUnchargedOrdersAppliedCreditIsReturnedOnce()
    {
        var order = UnfilledOrder(paymentType: PaymentType.Cash, paymentStatus: PaymentStatus.Pending);
        order.ApplyCredit(300m, UserId);
        Arrange(order);

        await Sweep();

        _refunds.Verify(
            r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyOrderEndedCreditReturn(order, Times.Once());
    }

    private void VerifyOrderEndedCreditReturn(Order order, Times times) =>
        _credit.Verify(c => c.TryReturnAsync(
            UserId,
            DefaultCurrencyId,
            300m,
            $"credit-return:order-ended-unpaid:{order.Id}",
            "system",
            It.IsAny<CancellationToken>(),
            order.Id,
            It.IsAny<string?>()), times);

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
            It.Is<Dictionary<string, string>>(args => args["amount"] == "250 Kč" && args["orderNumber"] == order.DisplayOrderNumber),
            It.IsAny<string?>(),
            order.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Owner ruling 2026-09-13: the push carries the credit WITH its currency, formatted
    /// from the credit's own currency row — a EUR apology says "10 €", never a CZK figure.
    /// </summary>
    [Fact]
    public async Task TheAmountIsStatedInTheCreditsOwnCurrency()
    {
        _eur.SetNoShowCredit(10m);
        var order = UnfilledOrder(currency: _eur);
        Arrange(order);

        await Sweep();

        _notifications.Verify(n => n.NotifyAsync(
            UserId,
            NotificationEventCatalog.OrderNoCleanerRefunded,
            It.Is<Dictionary<string, string>>(args => args["amount"] == "10 €"),
            It.IsAny<string?>(),
            order.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// THE HONESTY GUARD. The announcing key promises credit, so it is sent only when credit was
    /// actually issued. A currency with no authored figure is refused the grant and a guest has
    /// nowhere to hold it — both get the plain cancellation instead. Promising money nobody received
    /// would be worse than saying less.
    /// </summary>
    [Fact]
    public async Task ACustomerWhoGotNoCreditIsNotPromisedAny()
    {
        var order = UnfilledOrder(currency: _eur);
        Arrange(order);

        await Sweep();

        _notifications.Verify(n => n.NotifyAsync(
            UserId,
            NotificationEventCatalog.OrderCancelled,
            It.Is<Dictionary<string, string>>(args => !args.ContainsKey("amount")),
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

    /// <summary>
    /// A guest has no push and no feed, so the e-mail is the only word they get that nobody is coming. It
    /// retires every link they hold — the send mints the one that still opens the booking — and states the
    /// refund that actually went through.
    /// </summary>
    [Fact]
    public async Task AGuestIsEmailedWithTheirOldLinksRetiredAndTheRefundThatWentThrough()
    {
        var order = UnfilledOrder(userId: null);
        order.SetLanguage("cs");
        var oldLink = GuestOrderAccessToken.Issue(order.Id, DateTimeOffset.UtcNow.AddDays(3));
        _guestTokens.Setup(r => r.GetLiveForOrderIgnoringTenantAsync(
                order.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([oldLink]);
        Arrange(order);

        await Sweep();

        Assert.NotNull(oldLink.RevokedOn);
        var (queue, key, message) = Assert.Single(_enqueued);
        Assert.Equal(QueueNames.SendEmail, queue);
        Assert.Equal(MessageKeys.GuestOrderCancelledEmail(order.Id), key);
        var email = Assert.IsType<QueueEnvelope<SendGuestOrderCancellationEmailMessage>>(message).Payload;
        Assert.Equal(order.Id, email.OrderId);
        Assert.Equal("cs", email.LanguageCode);
        Assert.Equal(1000m, email.SuccessfulRefundAmount);
    }

    [Fact]
    public async Task AGuestWhoseRefundDidNotGoThroughIsNotToldTheyWereRefunded()
    {
        var order = UnfilledOrder(userId: null);
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundFailed)));
        Arrange(order);

        await Sweep();

        var (_, _, message) = Assert.Single(_enqueued);
        var email = Assert.IsType<QueueEnvelope<SendGuestOrderCancellationEmailMessage>>(message).Payload;
        Assert.Null(email.SuccessfulRefundAmount);
        Assert.Equal(Constants.Language.English, email.LanguageCode);
    }

    [Fact]
    public async Task AnAccountHolderIsToldByTheirOwnNotificationAndGetsNoGuestEmail()
    {
        Arrange(UnfilledOrder());

        await Sweep();

        Assert.Empty(_enqueued);
        _guestTokens.Verify(r => r.GetLiveForOrderIgnoringTenantAsync(
            It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The push says what happened to the money. A card refund that did not go through is pending,
    /// never "refunded in full" — the hourly re-drive owns it.
    /// </summary>
    [Fact]
    public async Task A_Card_Refund_That_Did_Not_Go_Through_Is_Announced_As_Pending()
    {
        var order = UnfilledOrder();
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundFailed)));
        Arrange(order);

        await Sweep();

        VerifyAnnounced(order, NotificationEventCatalog.OrderNoCleanerRefundPending);
    }

    [Fact]
    public async Task A_Stripe_Outage_Is_Announced_As_A_Pending_Refund()
    {
        var order = UnfilledOrder();
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection reset"));
        Arrange(order);

        await Sweep();

        VerifyAnnounced(order, NotificationEventCatalog.OrderNoCleanerRefundPending);
    }

    /// <summary>A cash booking took no money, so the push says nothing was charged.</summary>
    [Fact]
    public async Task A_Cash_Order_Is_Announced_As_Nothing_Charged()
    {
        var order = UnfilledOrder(paymentType: PaymentType.Cash, paymentStatus: PaymentStatus.Pending);
        Arrange(order);

        await Sweep();

        VerifyAnnounced(order, NotificationEventCatalog.OrderNoCleanerNothingCharged);
    }

    /// <summary>
    /// A Functions outage longer than six hours used to leave its orders unswept for good: the old
    /// lookback floor had moved past them by the first tick after it.
    /// </summary>
    [Fact]
    public async Task An_Order_Missed_For_Half_A_Day_Is_Still_Swept()
    {
        Arrange(BuildOrder(
            "order-missed", OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Paid,
            UserId, _czk, cleaningDateTime: DateTime.UtcNow.AddHours(-12)));

        var result = await Sweep();

        Assert.Equal(1, result.Value.CancelledCount);
    }

    /// <summary>
    /// Each order commits on its own, so a customer's second order never lands its raw credit return
    /// under a tracked balance from the first that a later commit would write back over it.
    /// </summary>
    [Fact]
    public async Task Each_Order_Commits_On_Its_Own()
    {
        Arrange(UnfilledOrder("order-a"), UnfilledOrder("order-b"));

        await Sweep();

        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private void VerifyAnnounced(Order order, string eventKey)
    {
        _notifications.Verify(n => n.NotifyAsync(
            UserId,
            eventKey,
            It.Is<Dictionary<string, string>>(args => args["amount"] == "250 Kč"),
            It.IsAny<string?>(),
            order.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.NotifyAsync(
            It.IsAny<string>(),
            It.Is<string>(key => key != eventKey),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class RecordingDispatch(List<(string Queue, string Key, object Message)> enqueued) : IPendingDispatch
    {
        public void Enqueue<T>(string queueName, T message, string messageKey) =>
            enqueued.Add((queueName, messageKey, message!));

        public IReadOnlyList<PendingMessage> Drain() => [];
    }

    private sealed class CapturingLogger<T>(List<(LogLevel Level, string Message)> entries)
        : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add((logLevel, formatter(state, exception)));
    }
}
