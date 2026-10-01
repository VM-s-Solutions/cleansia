using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The PaymentSheet intent keeps the card only when the customer ticked "save this card": the saved card is
/// recorded under the consent on the customer's Stripe Customer for the order's currency and its id goes to
/// Stripe with the intent, so the payment webhook completes it. Unticked, the intent asks Stripe to keep
/// nothing and no card is recorded.
/// </summary>
public class CreatePaymentIntentCardSavingTests
{
    private const string UserId = "user-sheet";
    private const string OrderId = "order-sheet";

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<IStripeCustomerResolver> _stripeCustomers = new();
    private readonly Mock<ISavedCardRepository> _savedCards = new();
    private readonly Mock<IRequestMetadataProvider> _requestMetadata = new();
    private readonly List<SavedCard> _addedCards = [];
    private readonly Currency _czk = Currency.Create("CZK", "Kč", "Czech koruna");
    private readonly User _user;

    public CreatePaymentIntentCardSavingTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _user = User.CreateWithPassword("sheet@example.com", "Passw0rd!", "Sh", "Eet");
        _user.Id = UserId;
        _user.AssignStripeCustomerId("cus_legacy");
        _users.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        var order = OrderMockFactory.Generate(
            new OrderMockFactory.OrderPartial
            {
                Id = OrderId,
                PaymentType = PaymentType.Card,
                UserId = UserId,
                CustomerAddress = AddressMockFactory.Generate(),
            },
            _czk);
        _orders.Setup(r => r.GetByIdForOwnerAsync(OrderId, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _stripeCustomers.Setup(r => r.ResolveForCurrencyAsync(_user, _czk, It.IsAny<CancellationToken>())).ReturnsAsync("cus_czk");
        _stripe
            .Setup(c => c.CreatePaymentIntentAsync(
                It.IsAny<decimal>(), "CZK", It.IsAny<string>(), OrderId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult("pi_sheet", "pi_sheet_secret"));
        _stripe.Setup(c => c.CreateEphemeralKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("ek_sheet");
        _savedCards.Setup(r => r.Add(It.IsAny<SavedCard>())).Callback<SavedCard>(_addedCards.Add);
        _requestMetadata.SetupGet(m => m.IpAddress).Returns("203.0.113.9");
        _requestMetadata.SetupGet(m => m.DeviceLabel).Returns("Pixel 8");
    }

    private CreatePaymentIntent.Handler Handler() => new(
        _orders.Object,
        _users.Object,
        _session.Object,
        _stripe.Object,
        new StripeConfig(new ConfigurationBuilder().Build()),
        _stripeCustomers.Object,
        _savedCards.Object,
        _requestMetadata.Object,
        NullLogger<CreatePaymentIntent.Handler>.Instance);

    [Fact]
    public async Task Ticked_The_Card_Is_Recorded_Under_The_Consent_And_Named_On_The_Intent()
    {
        var result = await Handler().Handle(new CreatePaymentIntent.Command(OrderId, SaveCard: true), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var card = Assert.Single(_addedCards);
        Assert.Equal(
            (UserId, _czk.Id, "cus_czk", SavedCard.ConsentTextVersionInForce, "203.0.113.9", "Pixel 8"),
            (card.UserId, card.CurrencyId, card.StripeCustomerId, card.ConsentTextVersion, card.ConsentIpAddress, card.ConsentDeviceLabel));
        Assert.False(card.IsCaptured);
        _stripe.Verify(c => c.CreatePaymentIntentAsync(
            It.IsAny<decimal>(), "CZK", "cus_czk", OrderId, It.IsAny<string>(), card.Id, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("cus_czk", result.Value!.StripeCustomerId);
    }

    [Fact]
    public async Task Unticked_Nothing_Is_Kept()
    {
        var result = await Handler().Handle(new CreatePaymentIntent.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(_addedCards);
        _stripe.Verify(c => c.CreatePaymentIntentAsync(
            It.IsAny<decimal>(), "CZK", "cus_legacy", OrderId, It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
        _stripeCustomers.Verify(
            r => r.ResolveForCurrencyAsync(It.IsAny<User>(), It.IsAny<Currency>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
