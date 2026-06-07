using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// A transient Stripe fault on any membership-command Stripe call must surface as an observable
/// <see cref="BusinessErrorMessage.PaymentGatewayUnavailable"/> business failure (a 4xx), not an
/// unhandled 500 — while a non-Stripe fault must still bubble so it is not masked as "gateway down".
/// The handlers each previously called Stripe bare; these tests pin every call site's catch.
/// </summary>
public class MembershipCommandsStripeFailureTests
{
    private const string UserId = "user-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string NewPlanCode = "PLUS_YEARLY";
    private const string StripeCustomerId = "cus_test_1";
    private const string StripePriceId = "price_test_1";
    private const string NewStripePriceId = "price_test_2";
    private const string SubscriptionId = "sub_test_1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();

    private readonly User _user;
    private readonly MembershipPlan _plan;

    public MembershipCommandsStripeFailureTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        _user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        _user.Id = UserId;
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user);

        _plan = MembershipPlan.Create(
            code: PlanCode,
            name: "Plus Monthly",
            monthlyPriceCzk: 199m,
            stripePriceId: StripePriceId,
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 0);
        _planRepository
            .Setup(r => r.GetByCodeAsync(PlanCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_plan);

        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
    }

    private static StripeException TransientStripeFault() =>
        new("stripe temporarily unavailable");

    private CreateMembershipSubscription.Handler SubscriptionHandler() =>
        new(
            _userRepository.Object,
            _membershipRepository.Object,
            _planRepository.Object,
            _session.Object,
            _stripe.Object,
            NullLogger<CreateMembershipSubscription.Handler>.Instance);

    private CreateMembershipCheckoutSession.Handler CheckoutHandler() =>
        new(
            _userRepository.Object,
            _membershipRepository.Object,
            _planRepository.Object,
            _session.Object,
            _stripe.Object,
            NullLogger<CreateMembershipCheckoutSession.Handler>.Instance);

    private SwapMembershipPlan.Handler SwapHandler() =>
        new(
            _membershipRepository.Object,
            _planRepository.Object,
            _session.Object,
            _stripe.Object,
            NullLogger<SwapMembershipPlan.Handler>.Instance);

    private CancelMembershipSubscription.Handler CancelHandler() =>
        new(
            _membershipRepository.Object,
            _session.Object,
            _stripe.Object,
            NullLogger<CancelMembershipSubscription.Handler>.Instance);

    private static CreateMembershipSubscription.Command ConfirmedCommand() =>
        new(PlanCode, PaymentMethodConfirmed: true);

    private static CreateMembershipSubscription.Command Phase1Command() =>
        new(PlanCode, PaymentMethodConfirmed: false);

    private void SetupActiveMembership()
    {
        var membership = UserMembership.Create(
            userId: UserId,
            membershipPlanId: _plan.Id,
            stripeSubscriptionId: SubscriptionId,
            currentPeriodStart: DateTime.UtcNow,
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        _planRepository
            .Setup(r => r.GetByCodeAsync(NewPlanCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MembershipPlan.Create(
                code: NewPlanCode,
                name: "Plus Yearly",
                monthlyPriceCzk: 1990m,
                stripePriceId: NewStripePriceId,
                discountPercentage: 10m,
                freeCancellationWindowHours: 8,
                allowsExpressUpgrade: true,
                billingInterval: BillingInterval.Yearly,
                trialPeriodDays: 0));
    }

    [Fact]
    public async Task CreateSubscription_CreateCustomerThrowsStripe_ReturnsPaymentGatewayUnavailable()
    {
        _stripe
            .Setup(c => c.CreateCustomerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(TransientStripeFault());

        var result = await SubscriptionHandler().Handle(Phase1Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
    }

    [Fact]
    public async Task CreateSubscription_CreateCustomerThrowsStripe_DoesNotAssignStripeCustomerId()
    {
        _stripe
            .Setup(c => c.CreateCustomerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(TransientStripeFault());

        await SubscriptionHandler().Handle(Phase1Command(), CancellationToken.None);

        Assert.Null(_user.StripeCustomerId);
    }

    [Fact]
    public async Task CreateSubscription_CreateSubscriptionThrowsStripe_ReturnsPaymentGatewayUnavailable()
    {
        _user.AssignStripeCustomerId(StripeCustomerId);
        _stripe
            .Setup(c => c.CreateSubscriptionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(TransientStripeFault());

        var result = await SubscriptionHandler().Handle(ConfirmedCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
    }

    [Fact]
    public async Task CreateSubscription_CreateSetupIntentThrowsStripe_ReturnsPaymentGatewayUnavailable()
    {
        _user.AssignStripeCustomerId(StripeCustomerId);
        _stripe
            .Setup(c => c.CreateSetupIntentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(TransientStripeFault());

        var result = await SubscriptionHandler().Handle(Phase1Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
    }

    [Fact]
    public async Task CreateSubscription_CreateEphemeralKeyThrowsStripe_ReturnsPaymentGatewayUnavailable()
    {
        _user.AssignStripeCustomerId(StripeCustomerId);
        _stripe
            .Setup(c => c.CreateSetupIntentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SetupIntentResult("seti_1", "seti_secret_1"));
        _stripe
            .Setup(c => c.CreateEphemeralKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(TransientStripeFault());

        var result = await SubscriptionHandler().Handle(Phase1Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
    }

    [Fact]
    public async Task CreateSubscription_NonStripeFault_Bubbles()
    {
        _stripe
            .Setup(c => c.CreateCustomerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("misconfigured DI"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SubscriptionHandler().Handle(Phase1Command(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateCheckoutSession_CreateCustomerThrowsStripe_ReturnsPaymentGatewayUnavailable()
    {
        _stripe
            .Setup(c => c.CreateCustomerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(TransientStripeFault());

        var result = await CheckoutHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode, "https://ok", "https://cancel"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
    }

    [Fact]
    public async Task CreateCheckoutSession_CreateSessionThrowsStripe_ReturnsPaymentGatewayUnavailable()
    {
        _user.AssignStripeCustomerId(StripeCustomerId);
        _stripe
            .Setup(c => c.CreateMembershipCheckoutSessionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(TransientStripeFault());

        var result = await CheckoutHandler().Handle(
            new CreateMembershipCheckoutSession.Command(PlanCode, "https://ok", "https://cancel"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
    }

    [Fact]
    public async Task CreateCheckoutSession_NonStripeFault_Bubbles()
    {
        _user.AssignStripeCustomerId(StripeCustomerId);
        _stripe
            .Setup(c => c.CreateMembershipCheckoutSessionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("misconfigured DI"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CheckoutHandler().Handle(
                new CreateMembershipCheckoutSession.Command(PlanCode, "https://ok", "https://cancel"),
                CancellationToken.None));
    }

    [Fact]
    public async Task SwapPlan_SwapThrowsStripe_ReturnsPaymentGatewayUnavailable()
    {
        SetupActiveMembership();
        _stripe
            .Setup(c => c.SwapSubscriptionPriceAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(TransientStripeFault());

        var result = await SwapHandler().Handle(
            new SwapMembershipPlan.Command(NewPlanCode), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
    }

    [Fact]
    public async Task SwapPlan_NonStripeFault_Bubbles()
    {
        SetupActiveMembership();
        _stripe
            .Setup(c => c.SwapSubscriptionPriceAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("misconfigured DI"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SwapHandler().Handle(new SwapMembershipPlan.Command(NewPlanCode), CancellationToken.None));
    }

    [Fact]
    public async Task CancelSubscription_CancelThrowsStripe_ReturnsPaymentGatewayUnavailable()
    {
        SetupActiveMembership();
        _stripe
            .Setup(c => c.CancelSubscriptionAtPeriodEndAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(TransientStripeFault());

        var result = await CancelHandler().Handle(
            new CancelMembershipSubscription.Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
    }

    [Fact]
    public async Task CancelSubscription_NonStripeFault_Bubbles()
    {
        SetupActiveMembership();
        _stripe
            .Setup(c => c.CancelSubscriptionAtPeriodEndAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("misconfigured DI"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CancelHandler().Handle(new CancelMembershipSubscription.Command(), CancellationToken.None));
    }
}
