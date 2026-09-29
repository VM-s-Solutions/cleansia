using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.SavedCards;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Features.Memberships;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.SavedCards;

/// <summary>
/// Owner ruling 2026-09-28, decision 16: a customer saves a card as the guarantee for cash bookings. Both
/// channels record the consent — the wording's version, the IP and the device — on a new row before the
/// card exists, open the capture on the Stripe Customer for the market's currency, and charge nothing.
/// Removing a card deactivates the caller's own row alone.
/// </summary>
public class SavedCardCaptureTests
{
    private const string UserId = "user-card-1";
    private const string EurCustomerId = "cus_eur_card";
    private const string IpAddress = "203.0.113.7";
    private const string DeviceLabel = "Pixel 8";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ISavedCardRepository> _savedCards = new();
    private readonly Mock<IStripeCustomerResolver> _customerResolver = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRequestMetadataProvider> _requestMetadata = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Currency _eur = MembershipPricingMockFactory.Eur();
    private readonly List<SavedCard> _added = [];

    public SavedCardCaptureTests()
    {
        var user = User.CreateWithPassword("card@example.com", "12345678Test!", "Card", "Holder");
        user.Id = UserId;
        _userRepository.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _customerResolver.Setup(r => r.ResolveForCurrencyAsync(user, _eur, It.IsAny<CancellationToken>())).ReturnsAsync(EurCustomerId);
        _requestMetadata.SetupGet(m => m.IpAddress).Returns(IpAddress);
        _requestMetadata.SetupGet(m => m.DeviceLabel).Returns(DeviceLabel);
        _savedCards.Setup(r => r.Add(It.IsAny<SavedCard>())).Callback<SavedCard>(_added.Add);
    }

    private CreateSavedCardCheckoutSession.Handler CheckoutHandler() => new(
        _userRepository.Object, _savedCards.Object, MarketResolution.Resolving(_eur).Object, _customerResolver.Object,
        _session.Object, _requestMetadata.Object, _stripe.Object, NullLogger<CreateSavedCardCheckoutSession.Handler>.Instance);

    private CreateSavedCardSetupIntent.Handler SetupIntentHandler() => new(
        _userRepository.Object, _savedCards.Object, MarketResolution.Resolving(_eur).Object, _customerResolver.Object,
        _session.Object, _requestMetadata.Object, _stripe.Object, NullLogger<CreateSavedCardSetupIntent.Handler>.Instance);

    private RemoveSavedCard.Handler RemoveHandler() => new(_savedCards.Object, _session.Object);

    private static void AssertConsentEvidence(SavedCard card)
    {
        Assert.Equal(UserId, card.UserId);
        Assert.Equal(MembershipPricingMockFactory.EurCurrencyId, card.CurrencyId);
        Assert.Equal(EurCustomerId, card.StripeCustomerId);
        Assert.Equal(SavedCard.ConsentTextVersionInForce, card.ConsentTextVersion);
        Assert.Equal(IpAddress, card.ConsentIpAddress);
        Assert.Equal(DeviceLabel, card.ConsentDeviceLabel);
        Assert.False(card.IsCaptured);
    }

    [Fact]
    public async Task The_Web_Capture_Records_The_Consent_And_Opens_A_Setup_Checkout_On_The_Currencys_Customer()
    {
        _stripe.Setup(c => c.CreateCardSetupCheckoutSessionAsync(EurCustomerId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string cardId, CancellationToken _) => $"https://checkout.stripe.test/{cardId}");

        var result = await CheckoutHandler().Handle(
            new CreateSavedCardCheckoutSession.Command(ConsentAccepted: true, CountryId: "country-svk"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var card = Assert.Single(_added);
        AssertConsentEvidence(card);
        Assert.Equal(card.Id, result.Value.SavedCardId);
        Assert.Equal($"https://checkout.stripe.test/{card.Id}", result.Value.CheckoutUrl);
    }

    [Fact]
    public async Task The_Mobile_Capture_Records_The_Consent_And_Opens_A_SetupIntent_For_PaymentSheet()
    {
        _stripe.Setup(c => c.CreateCardSetupIntentAsync(EurCustomerId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string cardId, CancellationToken _) => new SetupIntentResult($"seti_{cardId}", $"secret_{cardId}"));
        _stripe.Setup(c => c.CreateEphemeralKeyAsync(EurCustomerId, It.IsAny<CancellationToken>())).ReturnsAsync("ek_card");

        var result = await SetupIntentHandler().Handle(
            new CreateSavedCardSetupIntent.Command(ConsentAccepted: true, CountryId: "country-svk"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var card = Assert.Single(_added);
        AssertConsentEvidence(card);
        Assert.Equal(
            new CreateSavedCardSetupIntent.Response(card.Id, $"secret_{card.Id}", EurCustomerId, "ek_card"),
            result.Value);
    }

    [Fact]
    public async Task A_Stripe_Failure_Is_The_Gateway_Refusal_And_Records_No_Card()
    {
        _stripe.Setup(c => c.CreateCardSetupIntentAsync(EurCustomerId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("stripe down"));

        var result = await SetupIntentHandler().Handle(
            new CreateSavedCardSetupIntent.Command(ConsentAccepted: true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
        Assert.Empty(_added);
    }

    [Fact]
    public async Task Both_Captures_Refuse_Without_The_Consent()
    {
        var countries = Mock.Of<ICountryRepository>();

        var web = await new CreateSavedCardCheckoutSession.Validator(countries)
            .ValidateAsync(new CreateSavedCardCheckoutSession.Command(ConsentAccepted: false));
        var mobile = await new CreateSavedCardSetupIntent.Validator(countries)
            .ValidateAsync(new CreateSavedCardSetupIntent.Command(ConsentAccepted: false));

        Assert.Equal(BusinessErrorMessage.SavedCardConsentNotAccepted, Assert.Single(web.Errors).ErrorMessage);
        Assert.Equal(BusinessErrorMessage.SavedCardConsentNotAccepted, Assert.Single(mobile.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Another_Customers_Card_Answers_Not_Found_And_Stays()
    {
        var card = CapturedCard(userId: "user-someone-else");

        var result = await RemoveHandler().Handle(new RemoveSavedCard.Command(card.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.SavedCardNotFound, result.Error!.Message);
        _savedCards.Verify(r => r.Deactivate(It.IsAny<SavedCard>()), Times.Never);
    }

    [Fact]
    public async Task Removing_Ones_Own_Card_Deactivates_It()
    {
        var card = CapturedCard(userId: UserId);

        var result = await RemoveHandler().Handle(new RemoveSavedCard.Command(card.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _savedCards.Verify(r => r.Deactivate(card), Times.Once);
    }

    [Fact]
    public void Removing_A_Card_Never_Reaches_Stripe_Whose_Customer_Also_Bills_Plus()
    {
        var dependencies = typeof(RemoveSavedCard.Handler).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType);

        Assert.DoesNotContain(dependencies, t => t.Namespace == typeof(IStripeClient).Namespace);
    }

    private SavedCard CapturedCard(string userId)
    {
        var card = SavedCard.Start(userId, MembershipPricingMockFactory.EurCurrencyId, EurCustomerId, null, null);
        card.Capture("pm_card_1", "visa", "4242", 12, 2030);
        _savedCards.Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>())).ReturnsAsync(card);
        return card;
    }
}
