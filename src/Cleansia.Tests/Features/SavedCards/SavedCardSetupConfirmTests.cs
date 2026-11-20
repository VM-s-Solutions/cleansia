using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.SavedCards;
using Cleansia.Core.AppServices.Features.SavedCards.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.SavedCards;

/// <summary>
/// The app confirms a card it saved from Payments as soon as the sheet succeeds, so the card never waits on the
/// setup_intent.succeeded webhook. The server reads the SetupIntent from Stripe and completes only the caller's
/// own pending row on that intent's Customer, through the webhook's code; whichever of the two comes second
/// finds the card captured and changes nothing.
/// </summary>
public class SavedCardSetupConfirmTests
{
    private const string UserId = "user-confirm-1";
    private const string CustomerId = "cus_eur_confirm";
    private const string SetupIntentId = "seti_confirm_1";

    private static readonly SavedCardDetails Saved = new("pm_new", "visa", "4242", 9, 2031);

    private readonly Mock<ISavedCardRepository> _savedCards = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Currency _eur = MembershipPricingMockFactory.Eur();

    public SavedCardSetupConfirmTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _currencies.Setup(r => r.GetByIdAsync(_eur.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_eur);
        _savedCards.Setup(r => r.GetCapturedForUserInCurrencyAsync(UserId, _eur.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private Task<Cleansia.Infra.Common.Validations.BusinessResult<SavedCardDto>> ConfirmAsync() =>
        new ConfirmSavedCardSetup.Handler(
                _savedCards.Object, _session.Object, _stripe.Object, _currencies.Object,
                NullLogger<ConfirmSavedCardSetup.Handler>.Instance)
            .Handle(new ConfirmSavedCardSetup.Command(SetupIntentId), CancellationToken.None);

    private SavedCard PendingCard(string userId = UserId, string customerId = CustomerId)
    {
        var card = SavedCard.Start(userId, _eur.Id, customerId, null, null);
        _savedCards.Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>())).ReturnsAsync(card);
        return card;
    }

    private void StripeReads(string? customerId, string? savedCardId, SavedCardDetails? card) =>
        _stripe.Setup(c => c.GetSetupIntentCardAsync(SetupIntentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SetupIntentCard(customerId, savedCardId, card));

    [Fact]
    public async Task The_Callers_Succeeded_Setup_Lands_Its_Card_And_Answers_It_As_GetMine_Lists_It()
    {
        var card = PendingCard();
        StripeReads(CustomerId, card.Id, Saved);

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new SavedCardDto(card.Id, "visa", "4242", 9, 2031, "EUR"), result.Value);
        Assert.Equal("pm_new", card.StripePaymentMethodId);
        Assert.True(card.IsCaptured);
    }

    [Fact]
    public async Task The_Confirm_Retires_And_Detaches_The_Earlier_Card_In_That_Currency_As_The_Webhook_Does()
    {
        var card = PendingCard();
        var earlier = SavedCard.Start(UserId, _eur.Id, CustomerId, null, null);
        earlier.Capture("pm_old", "mastercard", "5454", 1, 2030);
        _savedCards.Setup(r => r.GetCapturedForUserInCurrencyAsync(UserId, _eur.Id, It.IsAny<CancellationToken>())).ReturnsAsync([earlier]);
        StripeReads(CustomerId, card.Id, Saved);

        await ConfirmAsync();

        _savedCards.Verify(r => r.Deactivate(earlier), Times.Once);
        _stripe.Verify(c => c.DetachSavedCardAsync(CustomerId, "pm_old", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Card_The_Webhook_Already_Landed_Is_Answered_As_It_Is_And_Nothing_Changes()
    {
        var card = PendingCard();
        card.Capture("pm_new", "visa", "4242", 9, 2031);
        StripeReads(CustomerId, card.Id, Saved);

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(card.Id, result.Value.Id);
        _savedCards.Verify(r => r.GetCapturedForUserInCurrencyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _savedCards.Verify(r => r.Deactivate(It.IsAny<SavedCard>()), Times.Never);
    }

    [Fact]
    public async Task Another_Customers_Setup_Answers_Not_Found_And_Lands_Nothing()
    {
        var card = PendingCard(userId: "user-someone-else");
        StripeReads(CustomerId, card.Id, Saved);

        await AssertRefusedAsync(BusinessErrorMessage.SavedCardNotFound);
        Assert.False(card.IsCaptured);
    }

    [Fact]
    public async Task A_Setup_On_Another_Stripe_Customer_Than_The_Row_Answers_Not_Found()
    {
        var card = PendingCard();
        StripeReads("cus_someone_else", card.Id, Saved);

        await AssertRefusedAsync(BusinessErrorMessage.SavedCardNotFound);
        Assert.False(card.IsCaptured);
    }

    [Fact]
    public async Task A_Setup_With_No_Saved_Card_Behind_It_Answers_Not_Found()
    {
        StripeReads(CustomerId, savedCardId: null, Saved);

        await AssertRefusedAsync(BusinessErrorMessage.SavedCardNotFound);
        _savedCards.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Setup_That_Has_Not_Succeeded_Answers_Not_Found_And_Leaves_The_Card_Pending()
    {
        var card = PendingCard();
        StripeReads(CustomerId, card.Id, card: null);

        await AssertRefusedAsync(BusinessErrorMessage.SavedCardNotFound);
        Assert.False(card.IsCaptured);
    }

    [Fact]
    public async Task A_Removed_Card_Answers_Not_Found()
    {
        var card = PendingCard();
        card.IsActive = false;
        StripeReads(CustomerId, card.Id, Saved);

        await AssertRefusedAsync(BusinessErrorMessage.SavedCardNotFound);
        Assert.False(card.IsCaptured);
    }

    [Fact]
    public async Task An_Unreachable_Stripe_Is_The_Gateway_Refusal()
    {
        _stripe.Setup(c => c.GetSetupIntentCardAsync(SetupIntentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("stripe down"));

        await AssertRefusedAsync(BusinessErrorMessage.PaymentGatewayUnavailable);
    }

    [Fact]
    public async Task An_Unreachable_Stripe_While_Detaching_The_Earlier_Card_Is_The_Gateway_Refusal_And_Lands_Nothing()
    {
        var card = PendingCard();
        var earlier = SavedCard.Start(UserId, _eur.Id, CustomerId, null, null);
        earlier.Capture("pm_old", "mastercard", "5454", 1, 2030);
        _savedCards.Setup(r => r.GetCapturedForUserInCurrencyAsync(UserId, _eur.Id, It.IsAny<CancellationToken>())).ReturnsAsync([earlier]);
        _stripe.Setup(c => c.DetachSavedCardAsync(CustomerId, "pm_old", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("stripe down"));
        StripeReads(CustomerId, card.Id, Saved);

        await AssertRefusedAsync(BusinessErrorMessage.PaymentGatewayUnavailable);
        Assert.False(card.IsCaptured);
        _savedCards.Verify(r => r.Deactivate(It.IsAny<SavedCard>()), Times.Never);
    }

    [Fact]
    public async Task A_Confirm_Without_A_SetupIntent_Is_Refused_Before_Stripe()
    {
        var validation = await new ConfirmSavedCardSetup.Validator().ValidateAsync(new ConfirmSavedCardSetup.Command(""));

        Assert.Equal(BusinessErrorMessage.Required, Assert.Single(validation.Errors).ErrorMessage);
    }

    private async Task AssertRefusedAsync(string errorKey)
    {
        var result = await ConfirmAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(errorKey, result.Error!.Message);
    }
}
