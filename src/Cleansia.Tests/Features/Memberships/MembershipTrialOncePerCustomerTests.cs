using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// Owner ruling 2026-08-03: <b>one trial period per customer</b>, enforced by us regardless of whether
/// the Stripe dashboard also enforces it (T-0497 AC3).
///
/// <para>The fact "this customer has already had a trial" cannot live on the membership row: a
/// re-subscribe creates a NEW <see cref="UserMembership"/>, so anything keyed on the current row resets
/// on exactly the event the rule exists to catch. It lives in the user's membership HISTORY — any row,
/// any status, carrying a non-null <see cref="UserMembership.TrialEndsAtUtc"/> — read through
/// <see cref="IUserMembershipRepository.HasEverStartedTrialAsync"/>.</para>
///
/// <para>Both subscribe surfaces must ask, because they reach Stripe by different routes: mobile via
/// <see cref="CreateMembershipSubscription"/> (SetupIntent + confirmed subscribe) and web via
/// <see cref="CreateMembershipCheckoutSession"/> (Stripe-hosted Checkout). A gate on one of the two is
/// not a gate.</para>
/// </summary>
public class MembershipTrialOncePerCustomerTests
{
    private const string UserId = "user-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string StripeCustomerId = "cus_test_1";
    private const string StripePriceId = "price_test_1";
    private const int PlanTrialDays = 30;

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();

    private readonly MembershipPlan _plan = MembershipPlan.Create(
        code: PlanCode,
        name: "Plus Monthly",
        monthlyPriceCzk: 199m,
        stripePriceId: StripePriceId,
        discountPercentage: 5m,
        freeCancellationWindowHours: 4,
        allowsExpressUpgrade: true,
        billingInterval: BillingInterval.Monthly,
        trialPeriodDays: PlanTrialDays,
        expressUpgradesPerMonth: 2);

    public MembershipTrialOncePerCustomerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        var user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        user.Id = UserId;
        user.AssignStripeCustomerId(StripeCustomerId);
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _planRepository
            .Setup(r => r.GetByCodeAsync(PlanCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_plan);

        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
    }

    private void HasAlreadyTrialed(bool alreadyTrialed) =>
        _membershipRepository
            .Setup(r => r.HasEverStartedTrialAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alreadyTrialed);

    private MembershipTrialResolver Resolver() => new(_membershipRepository.Object);

    private CreateMembershipSubscription.Handler SubscribeHandler() =>
        new(
            _userRepository.Object,
            _membershipRepository.Object,
            _planRepository.Object,
            _session.Object,
            _stripe.Object,
            Resolver(),
            NullLogger<CreateMembershipSubscription.Handler>.Instance);

    private CreateMembershipCheckoutSession.Handler CheckoutHandler() =>
        new(
            _userRepository.Object,
            _membershipRepository.Object,
            _planRepository.Object,
            _session.Object,
            _stripe.Object,
            Resolver(),
            NullLogger<CreateMembershipCheckoutSession.Handler>.Instance);

    private GetMyMembership.Handler MyMembershipHandler() =>
        new(
            _membershipRepository.Object,
            _session.Object,
            Mock.Of<IExpressWaiverResolver>());

    private void SetupStripeSubscription(DateTime? trialEnd) =>
        _stripe
            .Setup(c => c.CreateSubscriptionAsync(
                StripeCustomerId, StripePriceId, It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionResult(
                SubscriptionId: "sub_1",
                CurrentPeriodStart: DateTime.UtcNow,
                CurrentPeriodEnd: DateTime.UtcNow.AddMonths(1),
                TrialEnd: trialEnd));

    // ── the resolver: the rule itself, in one place ──

    [Fact]
    public async Task FirstTimeSubscriber_GetsThePlansFullTrial()
    {
        HasAlreadyTrialed(false);

        var trial = await Resolver().ResolveForUserAsync(UserId, _plan, CancellationToken.None);

        Assert.Equal(PlanTrialDays, trial.Days);
        Assert.False(trial.AlreadyUsed);
    }

    [Fact]
    public async Task Resubscriber_GetsNoTrial()
    {
        HasAlreadyTrialed(true);

        var trial = await Resolver().ResolveForUserAsync(UserId, _plan, CancellationToken.None);

        Assert.Equal(0, trial.Days);
        Assert.True(trial.AlreadyUsed);
    }

    [Fact]
    public async Task PlanWithoutATrial_OffersNothingEvenToAFirstTimer()
    {
        HasAlreadyTrialed(false);
        var noTrialPlan = MembershipPlan.Create(
            code: "PLUS_YEARLY",
            name: "Plus Yearly",
            monthlyPriceCzk: 179m,
            stripePriceId: "price_yearly",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Yearly,
            trialPeriodDays: 0);

        var trial = await Resolver().ResolveForUserAsync(UserId, noTrialPlan, CancellationToken.None);

        Assert.Equal(0, trial.Days);
        Assert.False(trial.AlreadyUsed);
    }

    /// <summary>
    /// The whole point of keying on the history rather than on the row: the trialing enrolment is
    /// cancelled and a second one is created, and the marker still answers "yes".
    /// </summary>
    [Fact]
    public async Task TheMarkerIsReadFromHistory_NotFromTheCurrentMembershipRow()
    {
        HasAlreadyTrialed(true);
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var trial = await Resolver().ResolveForUserAsync(UserId, _plan, CancellationToken.None);

        Assert.Equal(0, trial.Days);
        _membershipRepository.Verify(
            r => r.HasEverStartedTrialAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── mobile: SetupIntent + confirmed subscribe ──

    [Fact]
    public async Task Mobile_FirstSubscribe_SendsThePlansTrialDaysToStripe()
    {
        HasAlreadyTrialed(false);
        SetupStripeSubscription(DateTime.UtcNow.AddDays(PlanTrialDays));

        var result = await SubscribeHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _stripe.Verify(c => c.CreateSubscriptionAsync(
            StripeCustomerId, StripePriceId, PlanTrialDays,
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Mobile_Resubscribe_SendsZeroTrialDaysToStripe()
    {
        HasAlreadyTrialed(true);
        SetupStripeSubscription(trialEnd: null);

        var result = await SubscribeHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _stripe.Verify(c => c.CreateSubscriptionAsync(
            StripeCustomerId, StripePriceId, 0,
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The write that makes the NEXT subscribe answer correctly. Without it the marker is null forever
    /// and the gate above is permanently open.
    /// </summary>
    [Fact]
    public async Task Mobile_FirstSubscribe_StampsTheTrialDeadlineStripeReturned()
    {
        var trialEnd = DateTime.UtcNow.AddDays(PlanTrialDays);
        HasAlreadyTrialed(false);
        SetupStripeSubscription(trialEnd);

        UserMembership? added = null;
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(m => added = m);

        var result = await SubscribeHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(trialEnd, added!.TrialEndsAtUtc);
        Assert.True(added.IsInTrial);
    }

    [Fact]
    public async Task Mobile_Resubscribe_StampsNoTrialDeadline()
    {
        HasAlreadyTrialed(true);
        SetupStripeSubscription(trialEnd: null);

        UserMembership? added = null;
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(m => added = m);

        var result = await SubscribeHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Null(added!.TrialEndsAtUtc);
        Assert.False(added.IsInTrial);
    }

    // ── what the subscribe screen is allowed to advertise ──

    [Fact]
    public async Task MyMembership_TellsAFirstTimerTheTrialIsStillTheirs()
    {
        HasAlreadyTrialed(false);

        var result = await MyMembershipHandler().Handle(new GetMyMembership.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.TrialEligible);
    }

    /// <summary>
    /// Enforcing the rule without saying so turns the loop defect into the false-price defect: the
    /// screen keeps promising a free trial the server has already decided to refuse.
    /// </summary>
    [Fact]
    public async Task MyMembership_TellsAReturningCustomerTheTrialIsGone()
    {
        HasAlreadyTrialed(true);

        var result = await MyMembershipHandler().Handle(new GetMyMembership.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.TrialEligible);
    }

    // ── web: Stripe-hosted Checkout ──

    [Fact]
    public async Task Web_FirstCheckout_SendsThePlansTrialDaysToStripe()
    {
        HasAlreadyTrialed(false);
        _stripe
            .Setup(c => c.CreateMembershipCheckoutSessionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://checkout.stripe.test/s/1");

        var result = await CheckoutHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode, "https://ok", "https://no"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _stripe.Verify(c => c.CreateMembershipCheckoutSessionAsync(
            StripeCustomerId, StripePriceId, UserId, PlanCode, PlanTrialDays,
            "https://ok", "https://no", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Web_ReturningCustomersCheckout_SendsZeroTrialDaysToStripe()
    {
        HasAlreadyTrialed(true);
        _stripe
            .Setup(c => c.CreateMembershipCheckoutSessionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://checkout.stripe.test/s/2");

        var result = await CheckoutHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode, "https://ok", "https://no"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _stripe.Verify(c => c.CreateMembershipCheckoutSessionAsync(
            StripeCustomerId, StripePriceId, UserId, PlanCode, 0,
            "https://ok", "https://no", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
