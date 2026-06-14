using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
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

    public OrderPaymentDispatcherTests()
    {
        _stripeClientFactory.Setup(f => f.CreateClient()).Returns(_stripeClient.Object);
    }

    private OrderPaymentDispatcher CreateDispatcher() =>
        new(_stripeClientFactory.Object, _pending.Object,
            NullLogger<OrderPaymentDispatcher>.Instance);

    private static Order BuildOrder(PaymentType paymentType) =>
        OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = OrderId,
            PaymentType = paymentType,
            TenantId = TenantId,
            CustomerAddress = AddressMockFactory.Generate(),
        });

    [Fact]
    public async Task Card_CreatesStripeSession_ReturnsSessionId_DoesNotEnqueue()
    {
        _stripeClient
            .Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cs_test_session");

        var result = await CreateDispatcher().DispatchAsync(
            BuildOrder(PaymentType.Card), LanguageCode, CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Equal("cs_test_session", result.StripeSessionId);
        _pending.Verify(p => p.Enqueue(
            It.IsAny<string>(),
            It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(),
            It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Card_StripeException_ReturnsPaymentGatewayUnavailableFailure()
    {
        _stripeClient
            .Setup(c => c.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("stripe down"));

        var result = await CreateDispatcher().DispatchAsync(
            BuildOrder(PaymentType.Card), LanguageCode, CancellationToken.None);

        Assert.Null(result.StripeSessionId);
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
                BuildOrder(PaymentType.Card), LanguageCode, CancellationToken.None));
    }

    [Fact]
    public async Task Cash_EnqueuesReceiptAtOutboxSeam_ReturnsNullSession_NoStripeCall()
    {
        var result = await CreateDispatcher().DispatchAsync(
            BuildOrder(PaymentType.Cash), LanguageCode, CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Null(result.StripeSessionId);
        _pending.Verify(p => p.Enqueue(
            QueueNames.GenerateReceipt,
            It.Is<QueueEnvelope<GenerateReceiptMessage>>(e =>
                e.Payload.OrderId == OrderId
                && e.Payload.LanguageCode == LanguageCode),
            MessageKeys.Receipt(OrderId)),
            Times.Once);
        _stripeClient.Verify(
            c => c.CreateCheckoutSessionAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
