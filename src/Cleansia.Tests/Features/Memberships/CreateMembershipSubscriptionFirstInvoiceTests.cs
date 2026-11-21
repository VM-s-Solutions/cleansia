using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// A phone subscribe with no trial is charged at once, and the membership exists only once Stripe answers
/// that the first invoice is paid: a decline is refused with the card's reason and its subscription cancelled,
/// and a 3-D Secure step hands the app the invoice's PaymentIntent secret and grants nothing until a confirm
/// that finds the invoice paid. Stripe is modelled per attempt id, as its idempotency replays it.
/// </summary>
public class CreateMembershipSubscriptionFirstInvoiceTests
{
    private const string UserId = "user-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string StripeCustomerId = "cus_test_1";
    private const string StripePriceId = "price_test_1";
    private const string ClientToken = "attempt-1";
    private const string SubscriptionId = "sub_tok-attempt-1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();
    private readonly Mock<ICurrencyResolutionService> _currencyResolution = MarketResolution.Resolving();
    private readonly List<UserMembership> _added = [];
    private readonly List<(string UserId, string PlanCode, int TrialDays)> _subscribeCalls = [];
    private SubscriptionResult _stripeAnswer = Answer("active");

    public CreateMembershipSubscriptionFirstInvoiceTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        var user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        user.Id = UserId;
        user.AssignStripeCustomerId(StripeCustomerId);
        _userRepository.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        UsePlan(trialPeriodDays: 0);

        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>())).Callback<UserMembership>(_added.Add);
        _membershipRepository
            .Setup(r => r.GetByStripeSubscriptionIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => _added.SingleOrDefault(m => m.StripeSubscriptionId == id));
        _membershipRepository
            .Setup(r => r.GetLifecycleForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _added.SingleOrDefault());

        _stripe
            .Setup(c => c.CreateSubscriptionAsync(
                StripeCustomerId, StripePriceId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, string userId, string planCode, int trialDays, string _, CancellationToken _) =>
                _subscribeCalls.Add((userId, planCode, trialDays)))
            .ReturnsAsync(() => _stripeAnswer);
    }

    private void UsePlan(int trialPeriodDays)
    {
        var plan = MembershipPlan.Create(
            code: PlanCode,
            name: "Plus Monthly",
            discountPercentage: 5m,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: trialPeriodDays);
        _planRepository.Setup(r => r.GetByCodeAsync(PlanCode, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        _priceRepository.PriceIn(plan.Id, MembershipPricingMockFactory.CzkCurrencyId, StripePriceId);
    }

    private static SubscriptionResult Answer(
        string status, string? paymentIntentClientSecret = null, string? declineCode = null, DateTime? trialEnd = null) =>
        new(SubscriptionId, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), trialEnd, status, paymentIntentClientSecret, declineCode);

    private CreateMembershipSubscription.Handler CreateHandler() =>
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
            new StripeCustomerResolver(new Mock<IUserStripeCustomerRepository>().Object, _membershipRepository.Object,
                _stripe.Object, NullLogger<StripeCustomerResolver>.Instance),
            new AuditContext(),
            NullLogger<CreateMembershipSubscription.Handler>.Instance);

    private Task<Cleansia.Infra.Common.Validations.BusinessResult<CreateMembershipSubscription.Response>> Confirm() =>
        CreateHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: true) { IdempotencyToken = ClientToken },
            CancellationToken.None);

    [Fact]
    public async Task A_first_invoice_paid_at_once_grants_the_membership_and_tags_the_subscription_for_the_webhook()
    {
        var result = await Confirm();

        Assert.True(result.IsSuccess);
        var membership = Assert.Single(_added);
        Assert.Equal(membership.Id, result.Value.MembershipId);
        Assert.Equal(string.Empty, result.Value.PaymentIntentClientSecret);
        Assert.NotNull(membership.PaidPeriodConfirmedAt);
        Assert.Equal((UserId, PlanCode, 0), Assert.Single(_subscribeCalls));
        _stripe.Verify(c => c.CancelSubscriptionNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("card_declined", BusinessErrorMessage.MembershipCardDeclined)]
    [InlineData("do_not_honor", BusinessErrorMessage.MembershipCardDeclined)]
    [InlineData(null, BusinessErrorMessage.MembershipCardDeclined)]
    [InlineData("insufficient_funds", BusinessErrorMessage.MembershipCardInsufficientFunds)]
    [InlineData("payment_intent_authentication_failure", BusinessErrorMessage.MembershipCardAuthenticationFailed)]
    public async Task A_declined_first_invoice_is_refused_with_the_cards_reason_and_its_subscription_cancelled(
        string? declineCode, string expectedKey)
    {
        _stripeAnswer = Answer("incomplete", declineCode: declineCode);

        var result = await Confirm();

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedKey, result.Error!.Message);
        Assert.Empty(_added);
        _stripe.Verify(c => c.CancelSubscriptionNowAsync(SubscriptionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_decline_is_still_refused_when_stripe_cannot_cancel_the_subscription_now()
    {
        _stripeAnswer = Answer("incomplete", declineCode: "card_declined");
        _stripe
            .Setup(c => c.CancelSubscriptionNowAsync(SubscriptionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Stripe.StripeException("unreachable"));

        var result = await Confirm();

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.MembershipCardDeclined, result.Error!.Message);
        Assert.Empty(_added);
    }

    [Theory]
    [InlineData("canceled")]
    [InlineData("incomplete_expired")]
    public async Task A_subscription_that_has_ended_unpaid_is_refused_and_not_cancelled_again(string status)
    {
        _stripeAnswer = Answer(status, declineCode: "card_declined");

        var result = await Confirm();

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.MembershipCardDeclined, result.Error!.Message);
        Assert.Empty(_added);
        _stripe.Verify(c => c.CancelSubscriptionNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_3ds_step_returns_the_invoice_secret_and_grants_only_on_the_confirm_that_finds_it_paid()
    {
        _stripeAnswer = Answer("incomplete", paymentIntentClientSecret: "pi_1_secret_2");

        var challenge = await Confirm();

        Assert.True(challenge.IsSuccess);
        Assert.Equal("pi_1_secret_2", challenge.Value.PaymentIntentClientSecret);
        Assert.Equal(string.Empty, challenge.Value.MembershipId);
        Assert.Empty(_added);
        _stripe.Verify(c => c.CancelSubscriptionNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        var stillPending = await Confirm();

        Assert.True(stillPending.IsSuccess);
        Assert.Equal("pi_1_secret_2", stillPending.Value.PaymentIntentClientSecret);
        Assert.Empty(_added);

        _stripeAnswer = Answer("active");

        var paid = await Confirm();

        Assert.True(paid.IsSuccess);
        var membership = Assert.Single(_added);
        Assert.Equal(membership.Id, paid.Value.MembershipId);
        Assert.Equal(string.Empty, paid.Value.PaymentIntentClientSecret);
    }

    [Fact]
    public async Task A_retried_confirm_after_the_grant_replays_the_one_subscription_and_adds_no_second_membership()
    {
        _membershipRepository
            .Setup(r => r.GetLifecycleForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var first = await Confirm();
        var retry = await Confirm();

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        var membership = Assert.Single(_added);
        Assert.Equal(membership.Id, retry.Value.MembershipId);
        Assert.All(_subscribeCalls, call => Assert.Equal(UserId, call.UserId));
    }

    [Fact]
    public async Task A_trial_subscribe_is_granted_during_the_trial_as_before()
    {
        UsePlan(trialPeriodDays: 14);
        _stripeAnswer = Answer("trialing", trialEnd: DateTime.UtcNow.AddDays(14));

        var result = await Confirm();

        Assert.True(result.IsSuccess);
        var membership = Assert.Single(_added);
        Assert.NotNull(membership.TrialEndsAtUtc);
        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal((UserId, PlanCode, 14), Assert.Single(_subscribeCalls));
        _stripe.Verify(c => c.CancelSubscriptionNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
