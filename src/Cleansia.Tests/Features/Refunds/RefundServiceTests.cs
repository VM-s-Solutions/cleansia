using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Microsoft.EntityFrameworkCore;
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
    private readonly RecordingStripeClient _stripe = new();

    private RefundService CreateService() =>
        new(
            _refundRepository.Object,
            _orderRepository.Object,
            new StubStripeClientFactory(_stripe),
            NullLogger<RefundService>.Instance);

    private static Order CreateCardPaidOrder(decimal totalPrice)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: "user-1");
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AssignStripeSessionId(StripeSessionId);
        return order;
    }

    // A mobile (PaymentSheet) card order: T-0347 suppresses the Checkout Session, so the single
    // capturable charge surface is the PaymentIntent (StripeSessionId is empty).
    private static Order CreateMobileCardPaidOrder(decimal totalPrice)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: "user-1");
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AssignStripePaymentIntentId(StripePaymentIntentId);
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

        public Task<string> CreateCheckoutSessionAsync(Order order, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string> CreateCustomerAsync(string userId, string email, string fullName, string? phone, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PaymentIntentResult> CreatePaymentIntentAsync(decimal amount, string currency, string stripeCustomerId, string orderId, string displayOrderNumber, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task CancelPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string> CreateEphemeralKeyAsync(string stripeCustomerId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SetupIntentResult> CreateSetupIntentAsync(string stripeCustomerId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SubscriptionResult> CreateSubscriptionAsync(string stripeCustomerId, string stripePriceId, int trialPeriodDays, string idempotencyAttemptId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SubscriptionResult> SwapSubscriptionPriceAsync(string stripeSubscriptionId, string newStripePriceId, string idempotencyAttemptId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task CancelSubscriptionAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string> CreateMembershipCheckoutSessionAsync(string stripeCustomerId, string stripePriceId, string userId, string membershipPlanCode, int trialPeriodDays, string successUrl, string cancelUrl, string idempotencyAttemptId, CancellationToken cancellationToken)
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
