using Microsoft.Extensions.Configuration;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// Locks the B5 contract on <see cref="CreateMembershipCheckoutSession.Handler"/>: the
/// <see cref="BusinessErrorMessage.UserNotFound"/> failure names the OFFENDING field (the
/// session-derived user id), not <c>nameof(Command)</c> (consistency.md B5), mirroring the sibling
/// <see cref="CreateMembershipSubscription"/> handler. The web/Checkout success path still returns its
/// <see cref="CreateMembershipCheckoutSession.Response"/> shape.
/// </summary>
public class CreateMembershipCheckoutSessionContractLockTests
{
    private const string UserId = "user-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string StripeCustomerId = "cus_test_1";
    private const string StripePriceId = "price_test_1";
    private const string CheckoutUrl = "https://checkout.stripe.test/session_1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();
    private readonly Mock<ICurrencyResolutionService> _currencyResolution = MarketResolution.Resolving();

    public CreateMembershipCheckoutSessionContractLockTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var plan = MembershipPlan.Create(
            code: PlanCode,
            name: "Plus Monthly",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 0);
        _planRepository
            .Setup(r => r.GetByCodeAsync(PlanCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _priceRepository.PriceIn(plan.Id, MembershipPricingMockFactory.CzkCurrencyId, StripePriceId);
    }

    private void SetupUserWithStripeCustomer()
    {
        var user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        user.Id = UserId;
        user.AssignStripeCustomerId(StripeCustomerId);
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    private CreateMembershipCheckoutSession.Handler CreateHandler() =>
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
            NullLogger<CreateMembershipCheckoutSession.Handler>.Instance);

    [Fact]
    public async Task UserNotFound_Failure_NamesOffendingUserField_NotCommand()
    {
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.UserNotFound, result.Error!.Message);
        Assert.Equal("userId", result.Error.Code);
        Assert.NotEqual(nameof(CreateMembershipCheckoutSession.Command), result.Error.Code);
    }

    [Fact]
    public async Task MembershipAlreadyActive_Failure_NamesOffendingField_NotType()
    {
        var user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        user.Id = UserId;
        user.AssignStripeCustomerId(StripeCustomerId);
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var plan = MembershipPlan.Create(
            code: PlanCode, name: "Plus Monthly",
            discountPercentage: 5m, freeCancellationWindowHours: 4, allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly, trialPeriodDays: 0);
        var active = UserMembership.Create(UserId, plan.Id, "currency-czk", "sub_active_1", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(active);

        var result = await CreateHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipAlreadyActive, result.Error!.Message);
        Assert.Equal(nameof(CreateMembershipCheckoutSession.Command.PlanCode), result.Error.Code);
        Assert.NotEqual(nameof(UserMembership), result.Error.Code);
    }

    [Fact]
    public async Task HappyPath_ReturnsCheckoutUrl()
    {
        SetupUserWithStripeCustomer();
        _stripe
            .Setup(c => c.CreateMembershipCheckoutSessionAsync(
                StripeCustomerId, StripePriceId, UserId, PlanCode, It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckoutUrl);

        var result = await CreateHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CheckoutUrl, result.Value.CheckoutUrl);
    }

    [Fact]
    public async Task PlanUnpricedInTheResolvedCurrency_Refuses_AndNeverReachesStripe()
    {
        SetupUserWithStripeCustomer();
        _currencyResolution
            .Setup(s => s.ResolveCurrencyForCountryAsync("country-svk", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MembershipPricingMockFactory.Eur());

        var result = await CreateHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode, CountryId: "country-svk"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipPlanNotPricedInCurrency, result.Error!.Message);
        _stripe.Verify(c => c.CreateMembershipCheckoutSessionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheMarketsRow_IsWhatStripeIsHanded_NotAnotherCurrencys()
    {
        SetupUserWithStripeCustomer();
        var eur = MembershipPricingMockFactory.Eur();
        _currencyResolution
            .Setup(s => s.ResolveCurrencyForCountryAsync("country-svk", It.IsAny<CancellationToken>()))
            .ReturnsAsync(eur);
        var plan = await _planRepository.Object.GetByCodeAsync(PlanCode, CancellationToken.None);
        _priceRepository.PriceIn(plan!.Id, eur.Id, "price_eur_1", 7.99m);
        _stripe
            .Setup(c => c.CreateMembershipCheckoutSessionAsync(
                StripeCustomerId, "price_eur_1", UserId, PlanCode, It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CheckoutUrl);

        var result = await CreateHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode, CountryId: "country-svk"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _stripe.Verify(c => c.CreateMembershipCheckoutSessionAsync(
            StripeCustomerId, "price_eur_1", UserId, PlanCode, It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
