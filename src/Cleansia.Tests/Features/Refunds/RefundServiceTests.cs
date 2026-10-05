using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// The refund seam (ADR-0006 D1/D2/D3/D7): one place money leaves via Stripe, keyed on a
/// deterministic RefundKey, ceiling-clamped to the refundable amount, recording the Refund row +
/// payment-status transition after Stripe confirms, and collapsing a concurrent double-issue on the
/// unique key index.
///
/// Logic-level unit tests with mocked repositories + a fake Stripe client: the fast-path lookup is
/// the mocked GetByRefundKeyAsync, the consumed-ceiling read is the mocked
/// GetSucceededRefundTotalForOrderAsync, and the concurrent-race backstop is the mocked claim flush
/// throwing a wrapped 23505. A true-parallel proof against the real filtered unique index belongs to
/// the integration suite.
/// </summary>
public class RefundServiceTests
{
    private const string OrderId = "order-1";
    private const string ActorId = "admin-1";
    private const string StripeSessionId = "cs_test_123";
    private const string StripePaymentIntentId = "pi_test_456";

    private readonly Mock<IRefundRepository> _refundRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ICreditAccountRepository> _creditAccountRepository = new();
    private readonly RecordingStripeClient _stripe = new();

    private RefundService CreateService(ILogger<RefundService>? logger = null) =>
        new(
            _refundRepository.Object,
            _orderRepository.Object,
            _creditAccountRepository.Object,
            new StubStripeClientFactory(_stripe),
            logger ?? NullLogger<RefundService>.Instance);

    private static Order CreateCardPaidOrder(decimal totalPrice)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: "user-1",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AssignStripeSessionId(StripeSessionId);
        return order;
    }

    // A mobile (PaymentSheet) card order: T-0347 suppresses the Checkout Session, so the single
    // capturable charge surface is the PaymentIntent (StripeSessionId is empty).
    private static Order CreateMobileCardPaidOrder(decimal totalPrice)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: "user-1",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AssignStripePaymentIntentId(StripePaymentIntentId);
        return order;
    }

    /// <summary>A card-paid order loaded without its currency navigation -- what a projection that forgot the Include hands over.</summary>
    private static Order CreateCardPaidOrderWithoutCurrencyNavigation(decimal totalPrice)
    {
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Paid,
            userId: "user-1",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.AssignStripeSessionId(StripeSessionId);
        return order;
    }

    private void ArrangeOrder(Order order)
    {
        _orderRepository
            .Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
    }

    private void ArrangeNoExistingRefund()
    {
        _refundRepository
            .Setup(r => r.GetByRefundKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Refund?)null);
    }

    private void ArrangeConsumed(decimal consumed)
    {
        _refundRepository
            .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consumed);
    }

    private void ArrangeSettledInCredit(decimal settled)
    {
        _creditAccountRepository
            .Setup(r => r.GetDisputeSettledTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settled);
    }

    private static RefundRequest RequestFor(RefundReason reason, decimal amount) => reason switch
    {
        RefundReason.DisputeResolution => new RefundRequest(OrderId, amount, reason, ActorId, DisputeId: "dispute-2"),
        RefundReason.AdminDiscretion => new RefundRequest(OrderId, amount, reason, ActorId, RefundRequestId: "full"),
        _ => new RefundRequest(OrderId, amount, reason, ActorId),
    };

    private void CaptureAddedRefund(out List<Refund> added)
    {
        var captured = new List<Refund>();
        added = captured;
        _refundRepository
            .Setup(r => r.Add(It.IsAny<Refund>()))
            .Callback<Refund>(captured.Add);
        _refundRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task IssueRefund_OrderWithoutCurrencyNavigation_ThrowsNamingTheOrder_BeforeAnyRowOrStripeCall()
    {
        var order = CreateCardPaidOrderWithoutCurrencyNavigation(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out var added);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 1000m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None));

        Assert.Contains(OrderId, ex.Message);
        Assert.Empty(added);
        Assert.Equal(0, _stripe.RefundCallCount);
    }

    [Fact]
    public async Task IssueRefund_HappyPath_CallsStripeOnce_RecordsOneSucceededRow_AndFlipsPaymentStatus()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out var added);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 1000m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _stripe.RefundCallCount);
        var row = Assert.Single(added);
        Assert.Equal(RefundStatus.Succeeded, row.Status);
        Assert.Equal(1000m, row.Amount);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
    }

    [Fact]
    public async Task IssueRefund_QuantizesAmountToTwoDecimals_AtTheSeam_SoStripeAndLedgerAgree()
    {
        // A caller passing a >2dp amount (e.g. dispute resolution, whose validator only requires >= 0)
        // must not re-open the ledger<->Stripe cent divergence T-0355 kills: the Refund row
        // (numeric(18,2), rounds) and Stripe ((long)(amount*100), truncates) must see the SAME 2dp value.
        // The seam rounds in Refund.Create, so every caller is covered — not just CancelOrder.
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out var added);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 50.005m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(added);
        Assert.Equal(50.01m, row.Amount);          // persisted 2dp, away-from-zero (not 50.005)
        Assert.Equal(50.01m, _stripe.LastAmount);  // Stripe re-driven at the same 2dp value
        Assert.Equal(50.01m, result.Value!.Amount);
    }

    // Web non-regression (T-0348): a Checkout-Session order routes through the SESSION refund surface,
    // never the new PaymentIntent path.
    [Fact]
    public async Task IssueRefund_WebSessionOrder_RoutesThroughCheckoutSessionRefund_NotPaymentIntent()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out _);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 1000m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _stripe.SessionRefundCallCount);
        Assert.Equal(0, _stripe.PaymentIntentRefundCallCount);
        Assert.Equal(StripeSessionId, _stripe.LastSessionId);
    }

    // T-0348: a mobile-paid card order (StripeSessionId null, PaymentIntentId set) refunds in full via
    // the new PaymentIntent surface.
    [Fact]
    public async Task IssueRefund_MobilePaymentIntentOrder_FullRefund_RoutesThroughPaymentIntent()
    {
        var order = CreateMobileCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out var added);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 1000m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _stripe.PaymentIntentRefundCallCount);
        Assert.Equal(0, _stripe.SessionRefundCallCount);
        Assert.Equal(StripePaymentIntentId, _stripe.LastPaymentIntentId);
        var row = Assert.Single(added);
        Assert.Equal(RefundStatus.Succeeded, row.Status);
        Assert.Equal(1000m, row.Amount);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
    }

    // T-0348: a mobile-paid card order supports a partial refund on the PaymentIntent surface.
    [Fact]
    public async Task IssueRefund_MobilePaymentIntentOrder_PartialRefund_LeavesPartiallyRefunded()
    {
        var order = CreateMobileCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out var added);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 400m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _stripe.PaymentIntentRefundCallCount);
        Assert.Equal(400m, _stripe.LastAmount);
        Assert.Equal(400m, Assert.Single(added).Amount);
        Assert.Equal(PaymentStatus.PartiallyRefunded, order.PaymentStatus);
    }

    // T-0348: an order with NEITHER charge surface (no session, no intent) is not refundable — the
    // short-circuit is now keyed on "has a refundable surface", not on the Session alone.
    [Fact]
    public async Task IssueRefund_OrderWithNoChargeSurface_ReturnsNotRefundable_NoStripeCall()
    {
        var order = CreateMobileCardPaidOrder(1000m);
        order.AssignStripePaymentIntentId(string.Empty);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 1000m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundOrderNotRefundable, result.Error!.Message);
        Assert.Equal(0, _stripe.RefundCallCount);
    }

    [Fact]
    public async Task IssueRefund_PartialAmount_LeavesPaymentStatusPartiallyRefunded()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out var added);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 400m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(400m, Assert.Single(added).Amount);
        Assert.Equal(PaymentStatus.PartiallyRefunded, order.PaymentStatus);
    }

    [Fact]
    public async Task IssueRefund_AmountExceedsRefundable_IsClampedToTheCeiling()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(700m);
        CaptureAddedRefund(out var added);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 500m, RefundReason.AdminDiscretion, ActorId, RefundRequestId: "rr-1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(300m, result.Value!.Amount);
        Assert.Equal(300m, Assert.Single(added).Amount);
        Assert.Equal(300m, _stripe.LastAmount);
    }

    [Fact]
    public async Task IssueRefund_RefundKey_IsDeterministicPerPurpose()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out _);

        await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 100m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);
        var cancelKey1 = _stripe.LastIdempotencyKey;

        _stripe.Reset();
        await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 100m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);
        var cancelKey2 = _stripe.LastIdempotencyKey;

        Assert.Equal(cancelKey1, cancelKey2);
        Assert.Equal($"refund:{OrderId}:cancel", cancelKey1);
    }

    [Fact]
    public async Task IssueRefund_RefundKey_EncodesDisputeAndAdminPurposes()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out _);

        await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 100m, RefundReason.DisputeResolution, ActorId, DisputeId: "disp-9"),
            CancellationToken.None);
        Assert.Equal($"refund:{OrderId}:dispute:disp-9", _stripe.LastIdempotencyKey);

        _stripe.Reset();
        await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 100m, RefundReason.AdminDiscretion, ActorId, RefundRequestId: "rr-7"),
            CancellationToken.None);
        Assert.Equal($"refund:{OrderId}:admin:rr-7", _stripe.LastIdempotencyKey);
    }

    [Fact]
    public async Task IssueRefund_RetriedSameKey_AfterFirstSucceeded_ResolvesToExisting_NoSecondStripeRefund()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(0m);

        var existing = Refund.Create(
            OrderId, $"refund:{OrderId}:cancel", 400m, "CZK",
            RefundReason.CustomerCancellation, RefundSource.AppRefund);
        existing.MarkSucceeded("re_abc", DateTimeOffset.UtcNow);
        _refundRepository
            .Setup(r => r.GetByRefundKeyAsync($"refund:{OrderId}:cancel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 400m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ResolvedToExisting);
        Assert.Equal(0, _stripe.RefundCallCount);
        _refundRepository.Verify(r => r.Add(It.IsAny<Refund>()), Times.Never);
    }

    // A 1000 sale settled with 200 credit, 500 already back on the card: the card has 300 left. A
    // request for 800 splits 640 card / 160 credit, the card leg clamps to 300, and the credit leg's
    // 160 is read back from the ledger row its key wrote.
    [Fact]
    public async Task IssueRefund_ClampedByTheCardCeiling_ReportsTheCardLegAndTheCreditLegThatMoved()
    {
        var order = CreateCardPaidOrder(1000m);
        order.ApplyCredit(200m, "user-1");
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(500m);
        CaptureAddedRefund(out _);
        var creditKey = $"credit-return:refund:{OrderId}:dispute:dispute-1";
        var creditMoved = 0m;
        _creditAccountRepository
            .Setup(r => r.TryReturnAsync("user-1", order.CurrencyId, 160m, creditKey, ActorId,
                It.IsAny<CancellationToken>(), OrderId, null))
            .Callback(() => creditMoved = 160m)
            .ReturnsAsync(true);
        _creditAccountRepository
            .Setup(r => r.GetReturnedAmountAsync(creditKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => creditMoved);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 800m, RefundReason.DisputeResolution, ActorId, DisputeId: "dispute-1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(300m, result.Value!.Amount);
        Assert.Equal(300m, _stripe.LastAmount);
        Assert.Equal(160m, result.Value.CreditReturned);
    }

    [Fact]
    public async Task IssueRefund_ResolvingToAnExistingRefund_ReportsTheCreditItsKeyReturned()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(0m);
        var refundKey = $"refund:{OrderId}:dispute:dispute-1";
        var existing = Refund.Create(
            OrderId, refundKey, 300m, "CZK", RefundReason.DisputeResolution, RefundSource.AppRefund);
        existing.MarkSucceeded("re_abc", DateTimeOffset.UtcNow);
        _refundRepository
            .Setup(r => r.GetByRefundKeyAsync(refundKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _creditAccountRepository
            .Setup(r => r.GetReturnedAmountAsync($"credit-return:{refundKey}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(160m);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 800m, RefundReason.DisputeResolution, ActorId, DisputeId: "dispute-1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ResolvedToExisting);
        Assert.Equal(300m, result.Value.Amount);
        Assert.Equal(160m, result.Value.CreditReturned);
    }

    [Fact]
    public async Task IssueRefund_RetriedSameKey_AfterPriorStripeFailureLeftRowPending_ReDrivesStripe_NotPhantomResolve()
    {
        // Regression: a prior attempt whose Stripe call failed leaves a Pending row. A retry must
        // RE-DRIVE Stripe (the key is Stripe's idempotency key, so it issues exactly once) and report a
        // real success — it must NOT resolve-to-existing the Pending row as success (the phantom-refund
        // bug: money never moved but the caller would notify the customer it did). A new row is NOT added;
        // the existing Pending row is reused and marked Succeeded.
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(0m);

        var key = $"refund:{OrderId}:cancel";
        var pending = Refund.Create(
            OrderId, key, 400m, "CZK", RefundReason.CustomerCancellation, RefundSource.AppRefund);
        // status is Pending by construction (no MarkSucceeded)
        _refundRepository
            .Setup(r => r.GetByRefundKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        _refundRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 400m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.ResolvedToExisting);          // a REAL refund, not a phantom resolve
        Assert.Equal(1, _stripe.RefundCallCount);                 // Stripe WAS re-driven
        Assert.Equal(RefundStatus.Succeeded, pending.Status);     // the existing row is now succeeded
        Assert.Equal(PaymentStatus.PartiallyRefunded, order.PaymentStatus);
        _refundRepository.Verify(r => r.Add(It.IsAny<Refund>()), Times.Never); // reused, not a 2nd row
    }

    [Fact]
    public async Task IssueRefund_RedriveWithNonZeroConsumed_ClampsToLiveCeiling_NotTheStaleFrozenAmount()
    {
        // A prior Pending cancel refund froze 1000 (the full ceiling at the time). Since then a
        // DIFFERENT-purpose refund succeeded, consuming 700, so the live ceiling is now 1000 - 700 = 300.
        // Re-driving the stale 1000 would over-refund; the guard clamps the re-drive to the live ceiling
        // (T-0354). The existing re-drive test uses ArrangeConsumed(0m), leaving this cross-key gap untested.
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(700m);

        var key = $"refund:{OrderId}:cancel";
        var pending = Refund.Create(
            OrderId, key, 1000m, "CZK", RefundReason.CustomerCancellation, RefundSource.AppRefund);
        _refundRepository
            .Setup(r => r.GetByRefundKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        _refundRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 1000m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(300m, result.Value!.Amount);             // clamped, not the frozen 1000
        Assert.Equal(300m, pending.Amount);                    // the reused row was clamped down
        Assert.Equal(300m, _stripe.LastAmount);                // Stripe re-driven at the clamped amount
        Assert.Equal(RefundStatus.Succeeded, pending.Status);
    }

    [Fact]
    public async Task IssueRefund_ConcurrentDoubleIssue_LoserCatches23505_ResolvesToExisting_NoSecondStripeRefund()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(0m);

        // The loser's fast-path read sees nothing (winner not yet committed) — the TOCTOU window the
        // read alone cannot close. The claim flush then collides on the unique RefundKey index.
        var key = $"refund:{OrderId}:cancel";
        var winner = Refund.Create(
            OrderId, key, 400m, "CZK", RefundReason.CustomerCancellation, RefundSource.AppRefund);
        winner.MarkSucceeded("re_winner", DateTimeOffset.UtcNow);

        var reads = 0;
        _refundRepository
            .Setup(r => r.GetByRefundKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => reads++ == 0 ? null : winner);
        _refundRepository.Setup(r => r.Add(It.IsAny<Refund>()));
        _refundRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "duplicate key value violates unique constraint",
                new FakePostgresUniqueViolationException()));

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 400m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ResolvedToExisting);
        Assert.Equal(0, _stripe.RefundCallCount);
    }

    [Fact]
    public async Task IssueRefund_StripeFails_LeavesPaymentStatusUnflipped_RecordsNoSucceededRow_ReturnsFailure()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out var added);

        _stripe.ThrowOnRefund = true;

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 1000m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(RefundStatus.Pending, Assert.Single(added).Status);
    }

    [Fact]
    public async Task IssueRefund_ChargebackConsumedTheWholeCharge_ClampsToNothing_NoStripeRefund()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(1000m);
        CaptureAddedRefund(out _);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 1000m, RefundReason.AdminDiscretion, ActorId, RefundRequestId: "rr-2"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundNothingRefundable, result.Error!.Message);
        Assert.Equal(0, _stripe.RefundCallCount);
    }

    [Fact]
    public async Task IssueRefund_TransientStripeRetry_ReusesSameKey_DoesNotIssueSecondRefund()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out var added);

        // The resilience handler (inside the client boundary, ADR-0005 D1.2) auto-retries a transient
        // failure on this keyed write. The seam supplies one deterministic key, so every internal
        // attempt carries the SAME key and Stripe replays the one refund instead of issuing a second.
        _stripe.RetryFirstAttemptTransientlyInternally = true;

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 400m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _stripe.InternalAttemptCount);
        Assert.True(_stripe.AllRefundKeysIdentical);
        Assert.Equal(RefundStatus.Succeeded, Assert.Single(added).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssueRefund_SuppressedCredit_DoesNotIncreaseTheCardShareOrFailTheRefund(bool mobile)
    {
        var order = mobile ? CreateMobileCardPaidOrder(2000m) : CreateCardPaidOrder(2000m);
        order.ApplyCredit(500m, "user-1");
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out var added);
        var refundKey = $"refund:{OrderId}:cancel";
        _creditAccountRepository
            .Setup(r => r.TryReturnAsync("user-1", order.CurrencyId, 250m,
                $"credit-return:{refundKey}", ActorId, It.IsAny<CancellationToken>(), OrderId, null))
            .ReturnsAsync(false);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 1000m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(750m, result.Value!.Amount);
        Assert.Equal(750m, _stripe.LastAmount);
        Assert.Equal(1, _stripe.RefundCallCount);
        Assert.Equal(mobile ? 1 : 0, _stripe.PaymentIntentRefundCallCount);
        Assert.Equal(mobile ? 0 : 1, _stripe.SessionRefundCallCount);
        var refund = Assert.Single(added);
        Assert.Equal(750m, refund.Amount);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal(PaymentStatus.PartiallyRefunded, order.PaymentStatus);
        _creditAccountRepository.Verify(r => r.TryReturnAsync("user-1", order.CurrencyId, 250m,
            $"credit-return:{refundKey}", ActorId, It.IsAny<CancellationToken>(), OrderId, null), Times.Once);
    }

    [Fact]
    public async Task IssueRefund_SuppressedCredit_AllowsPendingCardRetryAndSucceededReplay()
    {
        var order = CreateCardPaidOrder(2000m);
        order.ApplyCredit(500m, "user-1");
        ArrangeOrder(order);
        ArrangeConsumed(0m);
        CaptureAddedRefund(out var added);
        var refundKey = $"refund:{OrderId}:cancel";
        _refundRepository
            .Setup(r => r.GetByRefundKeyAsync(refundKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => added.SingleOrDefault());
        _creditAccountRepository
            .Setup(r => r.TryReturnAsync("user-1", order.CurrencyId, 500m,
                $"credit-return:{refundKey}", ActorId, It.IsAny<CancellationToken>(), OrderId, null))
            .ReturnsAsync(false);
        var service = CreateService();
        var request = new RefundRequest(OrderId, 2000m, RefundReason.CustomerCancellation, ActorId);

        _stripe.ThrowOnRefund = true;
        var failed = await service.IssueRefundAsync(request, CancellationToken.None);

        Assert.True(failed.IsFailure);
        Assert.Equal(RefundStatus.Pending, Assert.Single(added).Status);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        _creditAccountRepository.Verify(r => r.TryReturnAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);

        _stripe.ThrowOnRefund = false;
        var retried = await service.IssueRefundAsync(request, CancellationToken.None);
        var replayed = await service.IssueRefundAsync(request, CancellationToken.None);

        Assert.True(retried.IsSuccess);
        Assert.False(retried.Value!.ResolvedToExisting);
        Assert.Equal(1500m, retried.Value.Amount);
        Assert.True(replayed.IsSuccess);
        Assert.True(replayed.Value!.ResolvedToExisting);
        Assert.Equal(retried.Value.RefundId, replayed.Value.RefundId);
        Assert.Equal(1500m, replayed.Value.Amount);
        Assert.Equal(1500m, _stripe.LastAmount);
        Assert.Equal(refundKey, _stripe.LastIdempotencyKey);
        Assert.Equal(1, _stripe.RefundCallCount);
        Assert.Equal(RefundStatus.Succeeded, Assert.Single(added).Status);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        _creditAccountRepository.Verify(r => r.TryReturnAsync("user-1", order.CurrencyId, 500m,
            $"credit-return:{refundKey}", ActorId, It.IsAny<CancellationToken>(), OrderId, null), Times.Once);
    }

    private Refund ArrangePendingRefund(Order order, decimal amount, string refundKey)
    {
        var refund = Refund.Create(order.Id, refundKey, amount, "CZK", RefundReason.ServiceNotRendered, RefundSource.AppRefund);
        _refundRepository.Setup(r => r.GetByIdAsync(refund.Id, It.IsAny<CancellationToken>())).ReturnsAsync(refund);
        _refundRepository.Setup(r => r.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return refund;
    }

    [Fact]
    public async Task Redrive_Calls_Stripe_On_The_Rows_Own_Key_And_Records_The_Refund()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(0m);
        var refund = ArrangePendingRefund(order, 1000m, $"refund:{OrderId}:admin");

        var result = await CreateService().RedriveAsync(refund.Id, "system", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _stripe.RefundCallCount);
        Assert.Equal($"refund:{OrderId}:admin", _stripe.LastIdempotencyKey);
        Assert.Equal(1000m, _stripe.LastAmount);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        _refundRepository.Verify(r => r.Add(It.IsAny<Refund>()), Times.Never);
    }

    /// <summary>
    /// The row keeps only the card leg, so the credit leg is read back through the same proportion:
    /// a full refund of a 2000 sale settled 500 by credit returns the 500, on the refund's own key.
    /// </summary>
    [Fact]
    public async Task Redrive_Returns_The_Credit_Leg_On_The_Refunds_Own_Key()
    {
        var order = CreateCardPaidOrder(2000m);
        order.ApplyCredit(500m, "user-1");
        ArrangeOrder(order);
        ArrangeConsumed(0m);
        var refundKey = $"refund:{OrderId}:admin";
        var refund = ArrangePendingRefund(order, 1500m, refundKey);

        var result = await CreateService().RedriveAsync(refund.Id, "system", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1500m, _stripe.LastAmount);
        _creditAccountRepository.Verify(r => r.TryReturnAsync("user-1", order.CurrencyId, 500m,
            $"credit-return:{refundKey}", "system", It.IsAny<CancellationToken>(), OrderId, null), Times.Once);
    }

    /// <summary>A credit leg the first attempt already gave back is not given back again.</summary>
    [Fact]
    public async Task Redrive_Does_Not_Return_Credit_That_Already_Came_Back()
    {
        var order = CreateCardPaidOrder(2000m);
        order.ApplyCredit(500m, "user-1");
        ArrangeOrder(order);
        ArrangeConsumed(0m);
        _creditAccountRepository
            .Setup(r => r.GetReturnedTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(500m);
        var refund = ArrangePendingRefund(order, 1500m, $"refund:{OrderId}:admin");

        var result = await CreateService().RedriveAsync(refund.Id, "system", CancellationToken.None);

        Assert.True(result.IsSuccess);
        _creditAccountRepository.Verify(r => r.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Redrive_Clamps_A_Stale_Amount_To_What_Is_Still_Refundable()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(400m);
        var refund = ArrangePendingRefund(order, 1000m, $"refund:{OrderId}:admin");

        var result = await CreateService().RedriveAsync(refund.Id, "system", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(600m, _stripe.LastAmount);
        Assert.Equal(600m, refund.Amount);
    }

    [Fact]
    public async Task Redrive_Stripe_Still_Refuses_Leaves_The_Row_Pending()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(0m);
        var refund = ArrangePendingRefund(order, 1000m, $"refund:{OrderId}:admin");
        _stripe.ThrowOnRefund = true;

        var result = await CreateService().RedriveAsync(refund.Id, "system", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundFailed, result.Error!.Message);
        Assert.Equal(RefundStatus.Pending, refund.Status);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    /// <summary>
    /// Another refund has since given back everything the card took: the row is closed, so the hourly
    /// re-drive stops selecting it and nobody is told money is stuck that is not owed.
    /// </summary>
    [Fact]
    public async Task Redrive_With_Nothing_Left_To_Refund_Closes_The_Row()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(1000m);
        var refund = ArrangePendingRefund(order, 1000m, $"refund:{OrderId}:admin");

        var result = await CreateService().RedriveAsync(refund.Id, "system", CancellationToken.None);

        Assert.Equal(BusinessErrorMessage.RefundNothingRefundable, result.Error!.Message);
        Assert.Equal(RefundStatus.Failed, refund.Status);
        Assert.Equal(0, _stripe.RefundCallCount);
    }

    [Fact]
    public async Task Redrive_Of_A_Refund_That_Already_Went_Through_Does_Not_Call_Stripe()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        var refund = ArrangePendingRefund(order, 1000m, $"refund:{OrderId}:admin");
        refund.MarkSucceeded(null, DateTimeOffset.UtcNow);

        var result = await CreateService().RedriveAsync(refund.Id, "system", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ResolvedToExisting);
        Assert.Equal(0, _stripe.RefundCallCount);
    }

    /// <summary>
    /// A 1000 sale with a complaint already settled in 300 of credit has 700 left to give back, whichever
    /// action asks for the whole price.
    /// </summary>
    [Theory]
    [InlineData(RefundReason.AdminDiscretion)]
    [InlineData(RefundReason.DisputeResolution)]
    [InlineData(RefundReason.ServiceNotRendered)]
    [InlineData(RefundReason.CustomerCancellation)]
    public async Task IssueRefund_AfterAComplaintSettledInCredit_SendsTheCardOnlyWhatTheSaleHasLeft(RefundReason reason)
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        ArrangeSettledInCredit(300m);
        CaptureAddedRefund(out var added);

        var result = await CreateService().IssueRefundAsync(RequestFor(reason, 1000m), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, _stripe.RefundCallCount);
        Assert.Equal(700m, _stripe.LastAmount);
        Assert.Equal(700m, result.Value!.Amount);
        var row = Assert.Single(added);
        Assert.Equal(700m, row.Amount);
        Assert.Equal(RefundStatus.Succeeded, row.Status);
        Assert.Equal(PaymentStatus.PartiallyRefunded, order.PaymentStatus);
    }

    [Theory]
    [InlineData(RefundReason.AdminDiscretion)]
    [InlineData(RefundReason.DisputeResolution)]
    [InlineData(RefundReason.ServiceNotRendered)]
    [InlineData(RefundReason.CustomerCancellation)]
    public async Task IssueRefund_WholePriceSettledInCredit_IsNothingRefundable_NoRowNoStripe(RefundReason reason)
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        ArrangeSettledInCredit(1000m);
        CaptureAddedRefund(out var added);

        var result = await CreateService().IssueRefundAsync(RequestFor(reason, 1000m), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundNothingRefundable, result.Error!.Message);
        Assert.Empty(added);
        Assert.Equal(0, _stripe.RefundCallCount);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        _creditAccountRepository.Verify(r => r.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task IssueRefund_RequestWithinWhatIsLeft_IsNotReduced()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        ArrangeSettledInCredit(300m);
        CaptureAddedRefund(out var added);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 400m, RefundReason.AdminDiscretion, ActorId, RefundRequestId: "rr-1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(400m, _stripe.LastAmount);
        Assert.Equal(400m, Assert.Single(added).Amount);
    }

    /// <summary>
    /// A 2000 sale paid 500 in credit and 1500 by card, with 400 already settled in credit, has 1600 left.
    /// It goes back in the mix it was paid in: 1200 to the card and 400 to the balance.
    /// </summary>
    [Fact]
    public async Task IssueRefund_CardAndCreditOrder_HeldSliceIsSplitInProportion()
    {
        var order = CreateCardPaidOrder(2000m);
        order.ApplyCredit(500m, "user-1");
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        ArrangeSettledInCredit(400m);
        CaptureAddedRefund(out var added);
        var creditKey = $"credit-return:refund:{OrderId}:admin:full";

        var result = await CreateService().IssueRefundAsync(
            RequestFor(RefundReason.AdminDiscretion, 2000m), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1200m, _stripe.LastAmount);
        Assert.Equal(1200m, Assert.Single(added).Amount);
        _creditAccountRepository.Verify(r => r.TryReturnAsync(
            "user-1", order.CurrencyId, 400m, creditKey, ActorId, It.IsAny<CancellationToken>(), OrderId, null), Times.Once);
        _creditAccountRepository.Verify(r => r.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        Assert.Equal(PaymentStatus.PartiallyRefunded, order.PaymentStatus);
    }

    [Fact]
    public async Task IssueRefund_PendingRowFromBeforeTheSettlement_IsClampedToTheHeldCardShare()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(0m);
        ArrangeSettledInCredit(300m);
        var key = $"refund:{OrderId}:cancel";
        var pending = Refund.Create(
            OrderId, key, 1000m, "CZK", RefundReason.CustomerCancellation, RefundSource.AppRefund);
        _refundRepository
            .Setup(r => r.GetByRefundKeyAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        _refundRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().IssueRefundAsync(
            new RefundRequest(OrderId, 1000m, RefundReason.CustomerCancellation, ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(700m, _stripe.LastAmount);
        Assert.Equal(700m, pending.Amount);
        Assert.Equal(RefundStatus.Succeeded, pending.Status);
        _refundRepository.Verify(r => r.Add(It.IsAny<Refund>()), Times.Never);
    }

    /// <summary>
    /// The dispute's own card refund went through and its resolution was lost, so the same resolution runs
    /// again. What is left now counts that very refund, so the replay must be answered with it before the
    /// hold is applied, or it would read as nothing refundable.
    /// </summary>
    [Fact]
    public async Task IssueRefund_ReplayOfItsOwnSucceededRefund_AfterASettlementInCredit_ResolvesToIt()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(600m);
        ArrangeSettledInCredit(400m);
        var refundKey = $"refund:{OrderId}:dispute:dispute-2";
        var own = Refund.Create(
                OrderId, refundKey, 600m, "CZK", RefundReason.DisputeResolution, RefundSource.AppRefund, disputeId: "dispute-2")
            .MarkSucceeded(stripeRefundId: null, confirmedOnUtc: DateTimeOffset.UtcNow);
        _refundRepository
            .Setup(r => r.GetByRefundKeyAsync(refundKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(own);

        var result = await CreateService().IssueRefundAsync(
            RequestFor(RefundReason.DisputeResolution, 600m), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(result.Value!.ResolvedToExisting);
        Assert.Equal(600m, result.Value.Amount);
        Assert.Equal(0, _stripe.RefundCallCount);
    }

    [Fact]
    public async Task Redrive_AfterASettlementInCredit_ClampsToWhatIsLeft()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(0m);
        ArrangeSettledInCredit(300m);
        var refund = ArrangePendingRefund(order, 1000m, $"refund:{OrderId}:admin");

        var result = await CreateService().RedriveAsync(refund.Id, "system", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(700m, _stripe.LastAmount);
        Assert.Equal(700m, refund.Amount);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal(PaymentStatus.PartiallyRefunded, order.PaymentStatus);
    }

    [Fact]
    public async Task Redrive_WhenASettlementCoveredThePrice_ClosesTheRow()
    {
        var order = CreateCardPaidOrder(1000m);
        ArrangeOrder(order);
        ArrangeConsumed(0m);
        ArrangeSettledInCredit(1000m);
        var refund = ArrangePendingRefund(order, 1000m, $"refund:{OrderId}:admin");

        var result = await CreateService().RedriveAsync(refund.Id, "system", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundNothingRefundable, result.Error!.Message);
        Assert.Equal(RefundStatus.Failed, refund.Status);
        Assert.Equal(0, _stripe.RefundCallCount);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    private void ArrangeCreditReturned(decimal forOrder, string refundKey, decimal onRefundKey)
    {
        _creditAccountRepository
            .Setup(r => r.GetReturnedTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(forOrder);
        _creditAccountRepository
            .Setup(r => r.GetReturnedAmountAsync($"credit-return:{refundKey}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(onRefundKey);
    }

    /// <summary>
    /// A member cancelled a 1000 sale paid 700 by card and 300 in credit, with 200 already settled in credit,
    /// while Stripe was down. The seam froze 560 on the card from the 800 left, and the credit share of that
    /// 800, 240, came back at once on the refund's own key. The re-drive refunds the same 560, so Stripe sees
    /// the same parameters on the same key, and 560 + 240 + 200 is the whole price.
    /// </summary>
    [Fact]
    public async Task Redrive_AfterASettlement_DoesNotCountItsOwnReturnedCreditLegAsGoneElsewhere()
    {
        var order = CreateCardPaidOrder(1000m);
        order.ApplyCredit(300m, "user-1");
        ArrangeOrder(order);
        ArrangeConsumed(0m);
        ArrangeSettledInCredit(200m);
        var refundKey = $"refund:{OrderId}:cancel";
        ArrangeCreditReturned(forOrder: 240m, refundKey, onRefundKey: 240m);
        var refund = ArrangePendingRefund(order, 560m, refundKey);

        var result = await CreateService().RedriveAsync(refund.Id, "system", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(560m, _stripe.LastAmount);
        Assert.Equal(560m, refund.Amount);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
    }

    /// <summary>
    /// An admin's full refund of 2000 (500 credit, 400 settled) went through at Stripe for 1200 and its credit
    /// leg of 400 came back, but the record of it was lost. The retry finds its row still pending and sends
    /// Stripe the same 1200 on the same key rather than a smaller amount Stripe would refuse.
    /// </summary>
    [Fact]
    public async Task IssueRefund_RetryAfterItsCreditLegCameBack_SendsStripeTheSameAmount()
    {
        var order = CreateCardPaidOrder(2000m);
        order.ApplyCredit(500m, "user-1");
        ArrangeOrder(order);
        ArrangeConsumed(0m);
        ArrangeSettledInCredit(400m);
        var refundKey = $"refund:{OrderId}:admin:full";
        ArrangeCreditReturned(forOrder: 400m, refundKey, onRefundKey: 400m);
        var pending = Refund.Create(
            OrderId, refundKey, 1200m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund);
        _refundRepository
            .Setup(r => r.GetByRefundKeyAsync(refundKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        _refundRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().IssueRefundAsync(
            RequestFor(RefundReason.AdminDiscretion, 2000m), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1200m, _stripe.LastAmount);
        Assert.Equal(1200m, pending.Amount);
        Assert.Equal(RefundStatus.Succeeded, pending.Status);
    }

    /// <summary>
    /// After the cancellation above returned 240 of credit on the refund's key, a second complaint was settled
    /// in 300 more. 1000 less the 500 settled leaves 500 for this refund, of which 240 has already come back
    /// in credit, so the card gets 260 and the customer ends with exactly the price.
    /// </summary>
    [Fact]
    public async Task Redrive_AfterAFurtherSettlement_CardTakesWhatIsLeftAfterTheCreditLegAlreadyReturned()
    {
        var order = CreateCardPaidOrder(1000m);
        order.ApplyCredit(300m, "user-1");
        ArrangeOrder(order);
        ArrangeConsumed(0m);
        ArrangeSettledInCredit(500m);
        var refundKey = $"refund:{OrderId}:cancel";
        ArrangeCreditReturned(forOrder: 240m, refundKey, onRefundKey: 240m);
        var refund = ArrangePendingRefund(order, 560m, refundKey);

        var result = await CreateService().RedriveAsync(refund.Id, "system", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(260m, _stripe.LastAmount);
        Assert.Equal(260m, refund.Amount);
    }

    /// <summary>
    /// A 1000 sale, 600 in credit and 400 by card, was cancelled while Stripe was down: all 600 of the credit
    /// came back on the refund's key. Since then another refund took 150 off the card and a complaint was
    /// settled in 50 of credit. The card ceiling leaves 250, but 600 of this refund is already back, so the
    /// card gets the 200 that brings the sale to exactly 1000, not 250.
    /// </summary>
    [Fact]
    public async Task Redrive_HoldsItsCardAndItsReturnedCreditLegTogether_AfterTheCardCeilingClamp()
    {
        var order = CreateCardPaidOrder(1000m);
        order.ApplyCredit(600m, "user-1");
        ArrangeOrder(order);
        ArrangeConsumed(150m);
        ArrangeSettledInCredit(50m);
        var refundKey = $"refund:{OrderId}:cancel";
        ArrangeCreditReturned(forOrder: 600m, refundKey, onRefundKey: 600m);
        var refund = ArrangePendingRefund(order, 400m, refundKey);

        var result = await CreateService().RedriveAsync(refund.Id, "system", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(200m, _stripe.LastAmount);
        Assert.Equal(200m, refund.Amount);
    }

    /// <summary>
    /// The customer's credit account is on the books of a company frozen for archive. A raw-SQL return would
    /// write a balance onto those sealed books past the frozen-books guard, so the credit leg is written off
    /// with a warning, as that company's credit is when it closes, and the card leg still goes back.
    /// </summary>
    [Fact]
    public async Task IssueRefund_CreditLegOnAFrozenCompanysBooks_IsWrittenOffWithAWarning_AndTheCardStillRefunds()
    {
        var order = CreateCardPaidOrder(2000m);
        order.ApplyCredit(500m, "user-1");
        ArrangeOrder(order);
        ArrangeNoExistingRefund();
        ArrangeConsumed(0m);
        CaptureAddedRefund(out var added);
        _creditAccountRepository
            .Setup(r => r.IsOnFrozenCompanyBooksAsync("user-1", order.CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var logger = new Mock<ILogger<RefundService>>();

        var result = await CreateService(logger.Object).IssueRefundAsync(
            RequestFor(RefundReason.DisputeResolution, 400m), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(300m, _stripe.LastAmount);
        Assert.Equal(RefundStatus.Succeeded, Assert.Single(added).Status);
        _creditAccountRepository.Verify(r => r.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        logger.Verify(l => l.Log(
            LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    private sealed class RecordingStripeClient : IStripeClient
    {
        private readonly List<string> _refundKeys = [];

        public int RefundCallCount { get; private set; }
        public int SessionRefundCallCount { get; private set; }
        public int PaymentIntentRefundCallCount { get; private set; }
        public int InternalAttemptCount { get; private set; }
        public string? LastIdempotencyKey { get; private set; }
        public string? LastSessionId { get; private set; }
        public string? LastPaymentIntentId { get; private set; }
        public decimal LastAmount { get; private set; }
        public bool RetryFirstAttemptTransientlyInternally { get; set; }
        public bool ThrowOnRefund { get; set; }
        public bool AllRefundKeysIdentical => _refundKeys.Distinct().Count() <= 1;

        public void Reset()
        {
            RefundCallCount = 0;
            SessionRefundCallCount = 0;
            PaymentIntentRefundCallCount = 0;
            InternalAttemptCount = 0;
            LastIdempotencyKey = null;
            LastSessionId = null;
            LastPaymentIntentId = null;
            LastAmount = 0m;
            _refundKeys.Clear();
            RetryFirstAttemptTransientlyInternally = false;
            ThrowOnRefund = false;
        }

        public Task RefundCheckoutSessionAsync(
            string stripeSessionId, decimal amount, string idempotencyKey, CancellationToken cancellationToken)
        {
            LastSessionId = stripeSessionId;
            SessionRefundCallCount++;
            return RecordRefund(amount, idempotencyKey);
        }

        public Task RefundPaymentIntentAsync(
            string paymentIntentId, decimal amount, string idempotencyKey, CancellationToken cancellationToken)
        {
            LastPaymentIntentId = paymentIntentId;
            PaymentIntentRefundCallCount++;
            return RecordRefund(amount, idempotencyKey);
        }

        private Task RecordRefund(decimal amount, string idempotencyKey)
        {
            if (ThrowOnRefund)
            {
                throw new StripeException("simulated Stripe refund failure");
            }

            _refundKeys.Add(idempotencyKey);
            InternalAttemptCount++;

            if (RetryFirstAttemptTransientlyInternally && InternalAttemptCount == 1)
            {
                _refundKeys.Add(idempotencyKey);
                InternalAttemptCount++;
            }

            RefundCallCount++;
            LastIdempotencyKey = idempotencyKey;
            LastAmount = amount;
            return Task.CompletedTask;
        }

        public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Order order, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Order order, DateTime expiresAtUtc, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string> CreateCustomerAsync(string userId, string email, string fullName, string? phone, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PaymentIntentResult> CreatePaymentIntentAsync(decimal amount, string currency, string stripeCustomerId, string orderId, string displayOrderNumber, string? savedCardId, string? currentPaymentIntentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task CancelPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task CancelReplacedPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<StripePaymentSnapshot> GetPaymentSnapshotAsync(string? stripeSessionId, string? stripePaymentIntentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string?> FindCheckoutSessionOrderIdAsync(string paymentIntentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string> CreateEphemeralKeyAsync(string stripeCustomerId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SetupIntentResult> CreateSetupIntentAsync(string stripeCustomerId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SetupIntentResult> CreateCardSetupIntentAsync(string stripeCustomerId, string savedCardId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string> CreateCardSetupCheckoutSessionAsync(string stripeCustomerId, string savedCardId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SavedCardDetails?> GetSetupIntentCardAsync(string setupIntentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SavedCardDetails?> GetPaymentIntentCardAsync(string paymentIntentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CheckoutSessionResult> CreateCardSavingCheckoutSessionAsync(Order order, string stripeCustomerId, string savedCardId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CheckoutSessionResult> CreateCardSavingCheckoutSessionAsync(Order order, DateTime expiresAtUtc, string stripeCustomerId, string savedCardId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string> ChargeReceivableOffSessionAsync(string receivableId, decimal amount, string currency, string stripeCustomerId, string paymentMethodId, int attempt, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CheckoutSessionResult> CreateReceivableCheckoutSessionAsync(string receivableId, string? currentSessionId, string orderId, string displayOrderNumber, decimal amount, string currency, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> ExpireReceivableCheckoutSessionAsync(string sessionId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SubscriptionResult> CreateSubscriptionAsync(string stripeCustomerId, string stripePriceId, int trialPeriodDays, string idempotencyAttemptId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SubscriptionResult> SwapSubscriptionPriceAsync(string stripeSubscriptionId, string newStripePriceId, string idempotencyAttemptId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task CancelSubscriptionAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task CancelSubscriptionNowAsync(string stripeSubscriptionId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string> CreateMembershipCheckoutSessionAsync(string stripeCustomerId, string stripePriceId, string userId, string membershipPlanCode, int trialPeriodDays, string idempotencyAttemptId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class StubStripeClientFactory(IStripeClient client) : IStripeClientFactory
    {
        public IStripeClient CreateClient() => client;
    }

    private sealed class FakePostgresUniqueViolationException : Exception
    {
        public string SqlState => "23505";
    }
}
