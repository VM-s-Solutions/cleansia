using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// Both subscribe surfaces hand Stripe the Customer the per-currency resolver answers with — never the
/// legacy <see cref="User.StripeCustomerId"/> directly (owner ruling 2026-09-13) — and a
/// Stripe failure while creating that Customer is the same gateway-unavailable refusal as before.
/// </summary>
public class MembershipSubscribeUsesPerCurrencyCustomerTests
{
    private const string UserId = "user-percur-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string LegacyCustomerId = "cus_legacy";
    private const string EurCustomerId = "cus_eur_2";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<IStripeCustomerResolver> _customerResolver = new();
    private readonly Currency _eur = MembershipPricingMockFactory.Eur();
    private readonly Mock<ICurrencyResolutionService> _currencyResolution;
    private readonly User _user;

    public MembershipSubscribeUsesPerCurrencyCustomerTests()
    {
        _currencyResolution = MarketResolution.Resolving(_eur);
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        _user = User.CreateWithPassword("percur@example.com", "12345678Test!", "Per", "Currency");
        _user.Id = UserId;
        _user.AssignStripeCustomerId(LegacyCustomerId);
        _userRepository.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(_user);

        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var plan = MembershipPlan.Create(PlanCode, "Plus Monthly", 5m, 4, true, BillingInterval.Monthly, 0);
        _planRepository.Setup(r => r.GetByCodeAsync(PlanCode, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        _priceRepository.PriceIn(plan.Id, _eur.Id, "price_eur_1", 7.99m);

        _customerResolver
            .Setup(r => r.ResolveForCurrencyAsync(_user, _eur, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EurCustomerId);
    }

    private CreateMembershipSubscription.Handler SubscribeHandler() =>
        new(
            _userRepository.Object,
            _membershipRepository.Object,
            _planRepository.Object,
            _priceRepository.Object,
            _currencyResolution.Object,
            _session.Object,
            _stripe.Object,
            new StripeConfig(new ConfigurationBuilder().Build()),
            new MembershipTrialResolver(_membershipRepository.Object),
            _customerResolver.Object,
            NullLogger<CreateMembershipSubscription.Handler>.Instance);

    private CreateMembershipCheckoutSession.Handler CheckoutHandler() =>
        new(
            _userRepository.Object,
            _membershipRepository.Object,
            _planRepository.Object,
            _priceRepository.Object,
            _currencyResolution.Object,
            _session.Object,
            _stripe.Object,
            new StripeConfig(new ConfigurationBuilder().Build()),
            new MembershipTrialResolver(_membershipRepository.Object),
            _customerResolver.Object,
            NullLogger<CreateMembershipCheckoutSession.Handler>.Instance);

    [Fact]
    public async Task The_Mobile_Subscribe_Charges_The_Currencys_Customer_Not_The_Legacy_One()
    {
        _stripe
            .Setup(c => c.CreateSubscriptionAsync(EurCustomerId, "price_eur_1", It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionResult("sub_eur_1", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));

        var result = await SubscribeHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: true, CountryId: "country-svk"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EurCustomerId, result.Value.StripeCustomerId);
        _stripe.Verify(c => c.CreateSubscriptionAsync(LegacyCustomerId, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_Mobile_Setup_Intent_Is_Opened_On_The_Currencys_Customer()
    {
        _stripe.Setup(c => c.CreateSetupIntentAsync(EurCustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SetupIntentResult("seti_1", "seti_secret_1"));
        _stripe.Setup(c => c.CreateEphemeralKeyAsync(EurCustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ek_secret_1");

        var result = await SubscribeHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: false, CountryId: "country-svk"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EurCustomerId, result.Value.StripeCustomerId);
        Assert.Equal("ek_secret_1", result.Value.EphemeralKey);
    }

    [Fact]
    public async Task The_Web_Checkout_Is_Opened_On_The_Currencys_Customer()
    {
        _stripe
            .Setup(c => c.CreateMembershipCheckoutSessionAsync(EurCustomerId, "price_eur_1", UserId, PlanCode, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://checkout.stripe.test/eur");

        var result = await CheckoutHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode, CountryId: "country-svk"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://checkout.stripe.test/eur", result.Value.CheckoutUrl);
        _stripe.Verify(c => c.CreateMembershipCheckoutSessionAsync(LegacyCustomerId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Stripe_Failure_Creating_The_Customer_Is_Gateway_Unavailable_On_Both_Surfaces()
    {
        _customerResolver
            .Setup(r => r.ResolveForCurrencyAsync(_user, _eur, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("customer creation failed"));

        var subscribe = await SubscribeHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: true, CountryId: "country-svk"),
            CancellationToken.None);
        var checkout = await CheckoutHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode, CountryId: "country-svk"),
            CancellationToken.None);

        Assert.True(subscribe.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, subscribe.Error!.Message);
        Assert.True(checkout.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, checkout.Error!.Message);
        _stripe.Verify(c => c.CreateSubscriptionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _stripe.Verify(c => c.CreateMembershipCheckoutSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
