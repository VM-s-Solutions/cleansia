using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// T-0689 follow-up — <c>Stripe:Enabled</c>, the switch that stops the platform taking new card payments.
///
/// <para><b>Why it exists.</b> There was no way to stop taking card payments at all: <c>IStripeConfig</c>
/// had five properties, all credentials and URLs, and the card branch called Stripe unconditionally. The
/// nearest thing to a control was a health probe that <i>reported</i> whether keys were present — and
/// missing keys mean Stripe calls fail at runtime, which is an outage, not a clean "off". Found while
/// deleting a feature-flag mechanism whose seeded rows advertised a <c>StripePayments</c> switch that
/// nothing read: the switch that name promised did not exist anywhere.</para>
///
/// <para><b>The gated surfaces are the three that MINT a charge:</b> the web checkout session
/// (<see cref="OrderPaymentDispatcher"/>), the resume-checkout session (<c>ResumeOrderCheckout</c>) and the
/// mobile PaymentSheet intent (<c>CreatePaymentIntent</c>). Cash is untouched, so the platform keeps
/// trading with the switch off.</para>
///
/// <para><b>Refunds are deliberately NOT gated</b>, and that is the load-bearing decision: switching card
/// payments off is precisely when outstanding charges need returning, so a switch that also froze
/// <c>RefundService</c> would trap customer money behind the incident it was flipped for.</para>
/// </summary>
public class CardPaymentsKillSwitchTests
{
    private const string LanguageCode = "en";

    private readonly Mock<IStripeClientFactory> _stripeClientFactory = new();
    private readonly Mock<IStripeClient> _stripeClient = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    public CardPaymentsKillSwitchTests()
    {
        _stripeClientFactory.Setup(f => f.CreateClient()).Returns(_stripeClient.Object);
    }

    private static IStripeConfig Config(bool? enabled) =>
        new StripeConfig(enabled is null
            ? new ConfigurationBuilder().Build()
            : new ConfigurationBuilder()
                .AddInMemoryCollection([
                    new KeyValuePair<string, string?>("Stripe:Enabled", enabled.Value ? "true" : "false")])
                .Build());

    private OrderPaymentDispatcher Dispatcher(bool? enabled, OrderChannel channel = OrderChannel.Web) =>
        new(_stripeClientFactory.Object, _pending.Object, new OrderChannelProvider(channel),
            Config(enabled), NullLogger<OrderPaymentDispatcher>.Instance);

    private static Order BuildOrder(PaymentType paymentType) =>
        OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = "order-killswitch",
            PaymentType = paymentType,
            TenantId = "tenant-1",
            CustomerAddress = AddressMockFactory.Generate(),
        });

    /// <summary>
    /// The default, and the direction that matters. An absent section means card payments WORK — the
    /// switch has to be typed to turn them off, never inferred from a missing value. That is the direction
    /// the retention job got wrong (T-0685), so it is pinned rather than assumed.
    /// </summary>
    [Fact]
    public async Task An_Absent_Setting_Leaves_Card_Payments_On()
    {
        _stripeClient
            .Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_on", "https://checkout.stripe.com/c/pay/cs_on"));

        var result = await Dispatcher(enabled: null)
            .DispatchAsync(BuildOrder(PaymentType.Card), LanguageCode, CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_on", result.CheckoutUrl);
    }

    /// <summary>Switched off, the web card path refuses and never reaches Stripe.</summary>
    [Fact]
    public async Task Disabled_Refuses_The_Web_Card_Path_Without_Calling_Stripe()
    {
        var result = await Dispatcher(enabled: false)
            .DispatchAsync(BuildOrder(PaymentType.Card), LanguageCode, CancellationToken.None);

        Assert.NotNull(result.Failure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Failure!.Message);
        _stripeClientFactory.Verify(f => f.CreateClient(), Times.Never);
    }

    /// <summary>
    /// The mobile channel mints nothing in the dispatcher — the PaymentSheet is its charge surface — but it
    /// must still refuse here, or "card payments off" would only hold on the web.
    /// </summary>
    [Fact]
    public async Task Disabled_Refuses_The_Mobile_Channel_Too()
    {
        var result = await Dispatcher(enabled: false, channel: OrderChannel.Mobile)
            .DispatchAsync(BuildOrder(PaymentType.Card), LanguageCode, CancellationToken.None);

        Assert.NotNull(result.Failure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Failure!.Message);
    }

    /// <summary>
    /// Cash keeps working with card payments off. Without this the switch would stop the business trading
    /// rather than stop it taking cards.
    /// </summary>
    [Fact]
    public async Task Disabled_Leaves_Cash_Orders_Working()
    {
        var result = await Dispatcher(enabled: false)
            .DispatchAsync(BuildOrder(PaymentType.Cash), LanguageCode, CancellationToken.None);

        Assert.Null(result.Failure);
        _pending.Verify(p => p.Enqueue(
            It.IsAny<string>(),
            It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(),
            It.IsAny<string>()),
            Times.Once);
    }

    /// <summary>The binding itself, both directions.</summary>
    [Fact]
    public void The_Section_Binds_True_By_Default_And_False_When_Set()
    {
        Assert.True(Config(null).Enabled);
        Assert.True(Config(true).Enabled);
        Assert.False(Config(false).Enabled);
    }
}
