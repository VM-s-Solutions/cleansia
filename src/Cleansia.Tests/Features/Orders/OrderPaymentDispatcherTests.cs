using Microsoft.Extensions.Configuration;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Unit tests for <see cref="OrderPaymentDispatcher"/> — the payment-side-effect collaborator extracted
/// from <c>CreateOrder.Handler</c>. Pins the Card flow's Stripe session creation + narrow
/// <c>StripeException</c> mapping (and non-Stripe bubble), and the Cash flow's post-commit outbox enqueue
/// at the <see cref="IPendingDispatch"/> seam with no Stripe call, so the extraction carries the same
/// behavior the handler characterization suite pins.
/// </summary>
public class OrderPaymentDispatcherTests
{
    private const string OrderId = "order-1";
    private const string TenantId = "tenant-1";
    private const string LanguageCode = "en";

    private readonly Mock<IStripeClientFactory> _stripeClientFactory = new();
    private readonly Mock<IStripeClient> _stripeClient = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IStripeCustomerResolver> _stripeCustomers = new();
    private readonly Mock<ISavedCardRepository> _savedCards = new();
    private readonly Mock<IRequestMetadataProvider> _requestMetadata = new();
    private readonly List<SavedCard> _addedCards = [];

    public OrderPaymentDispatcherTests()
    {
        _stripeClientFactory.Setup(f => f.CreateClient()).Returns(_stripeClient.Object);
        _savedCards.Setup(r => r.Add(It.IsAny<SavedCard>())).Callback<SavedCard>(_addedCards.Add);
    }

    private OrderPaymentDispatcher CreateDispatcher(OrderChannel channel = OrderChannel.Web) =>
        new(_stripeClientFactory.Object, _pending.Object,
            new OrderChannelProvider(channel),
            new StripeConfig(new ConfigurationBuilder().Build()),
            _users.Object,
            _stripeCustomers.Object,
            _savedCards.Object,
            _requestMetadata.Object,
            NullLogger<OrderPaymentDispatcher>.Instance);

    private (Order Order, Currency Currency) ArrangeSavingCustomer()
    {
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        var order = OrderMockFactory.Generate(
            new OrderMockFactory.OrderPartial
            {
                Id = OrderId,
                PaymentType = PaymentType.Card,
                TenantId = TenantId,
                UserId = "user-saving",
                CustomerAddress = AddressMockFactory.Generate(),
            },
            currency);
        var user = User.CreateWithPassword("saving@example.com", "Passw0rd!", "Sa", "Ving");
        user.Id = "user-saving";
        _users.Setup(r => r.GetByIdAsync("user-saving", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _stripeCustomers
            .Setup(r => r.ResolveForCurrencyAsync(user, currency, It.IsAny<CancellationToken>()))
            .ReturnsAsync("cus_czk");
        return (order, currency);
    }

    private static Order BuildOrder(PaymentType paymentType) =>
        OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = OrderId,
            PaymentType = paymentType,
            TenantId = TenantId,
            CustomerAddress = AddressMockFactory.Generate(),
        });

    [Fact]
    public async Task WebCard_CreatesStripeSession_RecordsItOnTheOrder_ReturnsUrl_DoesNotEnqueue()
    {
        _stripeClient
            .Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_test_session", "https://checkout.stripe.com/c/pay/cs_test_session"));

        var order = BuildOrder(PaymentType.Card);
        var result = await CreateDispatcher(OrderChannel.Web).DispatchAsync(
            order, LanguageCode, saveCard: false, CancellationToken.None);

        Assert.Null(result.Failure);
        // The dispatcher hands back the URL the browser is redirected to, and records the
        // session ID on the order as its charge surface — RefundService looks a web order's
        // session up by that id, and nothing used to write it.
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_session", result.CheckoutUrl);
        Assert.Equal("cs_test_session", order.StripeSessionId);
        _pending.Verify(p => p.Enqueue(
            It.IsAny<string>(),
            It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(),
            It.IsAny<string>()),
            Times.Never);
        Assert.Empty(_addedCards);
        _stripeClient.Verify(
            c => c.CreateCardSavingCheckoutSessionAsync(
                It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// A ticked "save this card" opens the card-saving session on the customer's Stripe Customer for the
    /// order's currency, and records the saved card with its consent before the redirect, as the profile's
    /// card capture does. The card itself lands when the payment webhook arrives.
    /// </summary>
    [Fact]
    public async Task WebCard_SavingTheCard_OpensTheSavingSession_AndRecordsTheConsentedCard()
    {
        var (order, currency) = ArrangeSavingCustomer();
        _requestMetadata.SetupGet(m => m.IpAddress).Returns("198.51.100.7");
        _requestMetadata.SetupGet(m => m.DeviceLabel).Returns("Firefox on Windows");
        _stripeClient
            .Setup(c => c.CreateCardSavingCheckoutSessionAsync(order, "cus_czk", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_saving", "https://checkout.stripe.com/c/pay/cs_saving"));

        var result = await CreateDispatcher(OrderChannel.Web).DispatchAsync(
            order, LanguageCode, saveCard: true, CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_saving", result.CheckoutUrl);
        Assert.Equal("cs_saving", order.StripeSessionId);
        var card = Assert.Single(_addedCards);
        Assert.Equal(
            ("user-saving", currency.Id, "cus_czk", SavedCard.ConsentTextVersionInForce, "198.51.100.7", "Firefox on Windows"),
            (card.UserId, card.CurrencyId, card.StripeCustomerId, card.ConsentTextVersion, card.ConsentIpAddress, card.ConsentDeviceLabel));
        Assert.False(card.IsCaptured);
        _stripeClient.Verify(
            c => c.CreateCardSavingCheckoutSessionAsync(order, "cus_czk", card.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _stripeClient.Verify(c => c.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The mobile apps save at their PaymentSheet intent; the booking itself keeps nothing.</summary>
    [Fact]
    public async Task MobileCard_SavingTheCard_RecordsNothingHere()
    {
        var result = await CreateDispatcher(OrderChannel.Mobile).DispatchAsync(
            BuildOrder(PaymentType.Card), LanguageCode, saveCard: true, CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Empty(_addedCards);
        _stripeCustomers.Verify(
            r => r.ResolveForCurrencyAsync(It.IsAny<User>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>A Stripe failure while saving refuses the booking as any card failure does, and records no card.</summary>
    [Fact]
    public async Task WebCard_SavingTheCard_StripeException_RecordsNoCard()
    {
        var (order, _) = ArrangeSavingCustomer();
        _stripeClient
            .Setup(c => c.CreateCardSavingCheckoutSessionAsync(
                It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("stripe down"));

        var result = await CreateDispatcher(OrderChannel.Web).DispatchAsync(
            order, LanguageCode, saveCard: true, CancellationToken.None);

        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Failure!.Message);
        Assert.Empty(_addedCards);
    }

    [Fact]
    public async Task MobileCard_DoesNotCreateStripeSession_ReturnsNullSession_DoesNotEnqueue()
    {
        var result = await CreateDispatcher(OrderChannel.Mobile).DispatchAsync(
            BuildOrder(PaymentType.Card), LanguageCode, saveCard: false, CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Null(result.CheckoutUrl);
        _stripeClient.Verify(
            c => c.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _pending.Verify(p => p.Enqueue(
            It.IsAny<string>(),
            It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(),
            It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task MobileCard_NeverInvokesStripeClientFactory()
    {
        await CreateDispatcher(OrderChannel.Mobile).DispatchAsync(
            BuildOrder(PaymentType.Card), LanguageCode, saveCard: false, CancellationToken.None);

        _stripeClientFactory.Verify(f => f.CreateClient(), Times.Never);
    }

    [Fact]
    public async Task Card_StripeException_ReturnsPaymentGatewayUnavailableFailure()
    {
        _stripeClient
            .Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("stripe down"));

        var result = await CreateDispatcher().DispatchAsync(
            BuildOrder(PaymentType.Card), LanguageCode, saveCard: false, CancellationToken.None);

        Assert.Null(result.CheckoutUrl);
        Assert.NotNull(result.Failure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Failure!.Message);
        Assert.Equal(nameof(PaymentType.Card), result.Failure.Code);
    }

    [Fact]
    public async Task Card_NonStripeException_Bubbles()
    {
        _stripeClient
            .Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bad order state"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateDispatcher().DispatchAsync(
                BuildOrder(PaymentType.Card), LanguageCode, saveCard: false, CancellationToken.None));
    }

    [Fact]
    public async Task Cash_EnqueuesTheBookingEmailAtOutboxSeam_NoReceipt_ReturnsNullSession_NoStripeCall()
    {
        var result = await CreateDispatcher().DispatchAsync(
            BuildOrder(PaymentType.Cash), LanguageCode, saveCard: false, CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Null(result.CheckoutUrl);
        _pending.Verify(p => p.Enqueue(
            QueueNames.SendEmail,
            It.Is<QueueEnvelope<SendOrderBookedEmailMessage>>(e =>
                e.Payload.OrderId == OrderId
                && e.Payload.LanguageCode == LanguageCode
                && e.Payload.ContractConcludedOn != null),
            MessageKeys.OrderBookedEmail(OrderId)),
            Times.Once);
        _pending.Verify(p => p.Enqueue(
            QueueNames.GenerateReceipt, It.IsAny<It.IsAnyType>(), It.IsAny<string>()),
            Times.Never);
        _stripeClient.Verify(
            c => c.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
