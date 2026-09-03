using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// <see cref="ResumeOrderCheckout"/> — the cancel page's second run at paying an order the customer
/// walked away from.
///
/// <para>This is a MONEY path and the rules that matter here are the refusals, so the validator is
/// tested state by state rather than through one happy path: the whole value of the feature is that
/// it cannot hand a checkout link to someone who does not own the order, cannot re-open a booking
/// that has already been paid for, and cannot give a mobile order a second capturable surface
/// alongside its PaymentSheet PaymentIntent.</para>
/// </summary>
public class ResumeOrderCheckoutTests
{
    private const string OrderId = "order-1";
    private const string OwnerId = "user-owner";
    private const string CheckoutUrl = "https://checkout.stripe.com/c/pay/cs_test_123";

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClientFactory> _stripeFactory = new();
    private readonly Mock<IStripeClient> _stripe = new();

    public ResumeOrderCheckoutTests()
    {
        _stripeFactory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        _session.Setup(s => s.GetUserId()).Returns(OwnerId);
        _stripe
            .Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckoutUrl);
    }

    private static Order BuildOrder(
        PaymentType paymentType = PaymentType.Card,
        PaymentStatus paymentStatus = PaymentStatus.Pending,
        OrderStatus currentStatus = OrderStatus.New,
        string? userId = OwnerId,
        string? paymentIntentId = null)
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = OrderId,
            UserId = userId,
            PaymentType = paymentType,
            PaymentStatus = paymentStatus,
            CurrentStatus = currentStatus,
            CustomerAddress = AddressMockFactory.Generate(),
        });
        if (!string.IsNullOrEmpty(paymentIntentId))
        {
            order.AssignStripePaymentIntentId(paymentIntentId);
        }
        return order;
    }

    private ResumeOrderCheckout.Validator CreateValidator(Order? order)
    {
        _orders
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        return new ResumeOrderCheckout.Validator(_orders.Object, _session.Object);
    }

    private ResumeOrderCheckout.Handler CreateHandler(Order order)
    {
        _orders
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        return new ResumeOrderCheckout.Handler(
            _orders.Object, _stripeFactory.Object, NullLogger<ResumeOrderCheckout.Handler>.Instance);
    }

    private async Task<string?> FirstErrorFor(Order? order)
    {
        var result = await CreateValidator(order)
            .ValidateAsync(new ResumeOrderCheckout.Command(OrderId));
        return result.IsValid ? null : result.Errors[0].ErrorMessage;
    }

    // ─── The refusals ──────────────────────────────────────────────────────

    [Fact]
    public async Task An_unpaid_card_order_owned_by_the_caller_passes()
    {
        Assert.Null(await FirstErrorFor(BuildOrder()));
    }

    /// <summary>
    /// A non-owner is told the order does not exist, not that it is not theirs. Anything else turns
    /// this endpoint into an oracle for which order ids are real.
    /// </summary>
    [Fact]
    public async Task Someone_elses_order_is_refused_as_not_found()
    {
        Assert.Equal(
            BusinessErrorMessage.OrderNotFound,
            await FirstErrorFor(BuildOrder(userId: "user-someone-else")));
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused_as_not_found()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, await FirstErrorFor(BuildOrder()));
    }

    [Fact]
    public async Task A_missing_order_is_refused_as_not_found()
    {
        Assert.Equal(BusinessErrorMessage.OrderNotFound, await FirstErrorFor(null));
    }

    /// <summary>
    /// The cascade stops at ownership, so a non-owner cannot learn an order's payment state by
    /// reading which refusal came back.
    /// </summary>
    [Fact]
    public async Task A_paid_order_belonging_to_someone_else_still_answers_not_found()
    {
        Assert.Equal(
            BusinessErrorMessage.OrderNotFound,
            await FirstErrorFor(BuildOrder(
                paymentStatus: PaymentStatus.Paid, userId: "user-someone-else")));
    }

    [Fact]
    public async Task A_cash_order_has_no_checkout_to_resume()
    {
        Assert.Equal(
            BusinessErrorMessage.InvalidEnumValue,
            await FirstErrorFor(BuildOrder(paymentType: PaymentType.Cash)));
    }

    [Theory]
    [InlineData(PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Refunded)]
    [InlineData(PaymentStatus.Disputed)]
    [InlineData(PaymentStatus.PartiallyRefunded)]
    public async Task An_order_whose_money_has_already_moved_is_refused(PaymentStatus status)
    {
        Assert.Equal(
            BusinessErrorMessage.OrderPaymentAlreadyPaid,
            await FirstErrorFor(BuildOrder(paymentStatus: status)));
    }

    /// <summary>
    /// A declined card leaves the order Failed, and that is exactly the customer this feature is
    /// for — nothing was captured, so there is still something to collect.
    /// </summary>
    [Fact]
    public async Task A_failed_payment_can_be_retried()
    {
        Assert.Null(await FirstErrorFor(BuildOrder(paymentStatus: PaymentStatus.Failed)));
    }

    /// <summary>
    /// `CleanupStalePendingOrders` cancels an abandoned card order an hour later. After that there
    /// is nothing to pay for, and offering a checkout link would take money for a booking that no
    /// longer exists.
    /// </summary>
    [Fact]
    public async Task A_cancelled_order_cannot_be_paid_for()
    {
        Assert.Equal(
            BusinessErrorMessage.OrderAlreadyCancelled,
            await FirstErrorFor(BuildOrder(currentStatus: OrderStatus.Cancelled)));
    }

    /// <summary>
    /// The one that would actually cost someone money twice: a mobile order holds a PaymentIntent as
    /// its single capturable surface, and handing it a Checkout Session as well gives one booking
    /// two independent ways to be charged.
    /// </summary>
    [Fact]
    public async Task An_order_holding_a_payment_intent_is_not_given_a_second_surface()
    {
        Assert.Equal(
            BusinessErrorMessage.InvalidOrderStatusTransition,
            await FirstErrorFor(BuildOrder(paymentIntentId: "pi_test_123")));
    }

    [Fact]
    public async Task An_empty_order_id_is_refused_before_anything_is_looked_up()
    {
        var result = await CreateValidator(BuildOrder())
            .ValidateAsync(new ResumeOrderCheckout.Command(string.Empty));
        Assert.Equal(BusinessErrorMessage.Required, result.Errors[0].ErrorMessage);
        _orders.Verify(
            r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── The handler ───────────────────────────────────────────────────────

    /// <summary>
    /// Asking Stripe again is the whole mechanism: session creation is keyed on
    /// <c>checkout-{orderId}</c>, so within the idempotency window Stripe replays the session the
    /// customer abandoned rather than minting a second one. The handler must therefore ask for a
    /// session in the ordinary way — anything cleverer would break the replay.
    /// </summary>
    [Fact]
    public async Task It_asks_stripe_for_the_orders_session_and_returns_its_url()
    {
        var order = BuildOrder();
        var result = await CreateHandler(order)
            .Handle(new ResumeOrderCheckout.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CheckoutUrl, result.Value.CheckoutUrl);
        _stripe.Verify(
            s => s.CreateCheckoutSessionAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_stripe_outage_maps_to_the_gateway_error_rather_than_a_500()
    {
        _stripe
            .Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("stripe is down"));

        var result = await CreateHandler(BuildOrder())
            .Handle(new ResumeOrderCheckout.Command(OrderId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
    }

    /// <summary>
    /// Narrow catch, matching <c>OrderPaymentDispatcher</c>: a DI misconfiguration or a null
    /// reference is a bug we want to see as a 500, not one dressed up as a transient outage.
    /// </summary>
    [Fact]
    public async Task A_non_stripe_failure_bubbles()
    {
        _stripe
            .Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateHandler(BuildOrder())
                .Handle(new ResumeOrderCheckout.Command(OrderId), CancellationToken.None));
    }
}
