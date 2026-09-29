using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Receivables;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Receivables;

/// <summary>
/// The customer's pay link for what they owe on an order (owner ruling 2026-09-28, decision 18). It takes no
/// off-session switch, so it works while those charges stay off. It is refused for another customer's
/// receivable, one no longer open, and whenever card payments are switched off or Stripe refuses.
/// </summary>
public sealed class CreateReceivablePayLinkTests
{
    private const string CustomerId = "customer-owing";

    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Mock<IStripeConfig> _stripeConfig = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Receivable _receivable;

    public CreateReceivablePayLinkTests()
    {
        _stripeConfig.SetupGet(c => c.Enabled).Returns(true);
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            UserId = CustomerId,
            PaymentType = PaymentType.Cash,
            PaymentStatus = PaymentStatus.Pending,
            TotalPrice = 1000m,
        });
        _receivable = Receivable.ForCashCancellationFee(order, 250m);
        typeof(Receivable).GetProperty(nameof(Receivable.Order))!.SetValue(_receivable, order);
        typeof(Receivable).GetProperty(nameof(Receivable.Currency))!.SetValue(_receivable, Currency.Create("CZK", "Kč", "Czech koruna"));
        _receivables
            .Setup(r => r.GetByIdIgnoringTenantAsync(_receivable.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_receivable);
        _stripe
            .Setup(c => c.CreateReceivableCheckoutSessionAsync(
                _receivable.Id, _receivable.OrderId, order.DisplayOrderNumber, 250m, "CZK", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckoutSessionResult("cs_link", "https://checkout.stripe.test/pay/link"));
    }

    private CreateReceivablePayLink.Handler Handler(string callerId = CustomerId)
    {
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(callerId);
        return new CreateReceivablePayLink.Handler(
            _receivables.Object, session.Object, _stripeConfig.Object, _stripe.Object,
            NullLogger<CreateReceivablePayLink.Handler>.Instance);
    }

    [Fact]
    public async Task The_Customer_Gets_A_Checkout_For_What_They_Owe_On_The_Order()
    {
        var result = await Handler().Handle(new CreateReceivablePayLink.Command(_receivable.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new CreateReceivablePayLink.Response(_receivable.Id, "https://checkout.stripe.test/pay/link"), result.Value);
    }

    [Fact]
    public async Task Another_Customers_Receivable_Is_Not_Found_And_No_Checkout_Is_Opened()
    {
        var result = await Handler("someone-else").Handle(new CreateReceivablePayLink.Command(_receivable.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReceivableNotFound, result.Error!.Message);
        _stripe.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Receivable_Written_Off_Is_Not_Payable()
    {
        _receivable.WriteOff("admin-1", "Goodwill", DateTimeOffset.UtcNow);

        var result = await Handler().Handle(new CreateReceivablePayLink.Command(_receivable.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReceivableNotOpen, result.Error!.Message);
        _stripe.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task With_Card_Payments_Switched_Off_No_Checkout_Is_Opened()
    {
        _stripeConfig.SetupGet(c => c.Enabled).Returns(false);

        var result = await Handler().Handle(new CreateReceivablePayLink.Command(_receivable.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
        _stripe.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Stripe_Refusal_Is_The_Gateway_Unavailable_Answer()
    {
        _stripe
            .Setup(c => c.CreateReceivableCheckoutSessionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("Stripe is unavailable"));

        var result = await Handler().Handle(new CreateReceivablePayLink.Command(_receivable.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
    }
}
