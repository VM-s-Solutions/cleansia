using System.Text.Json;
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
/// ADR-0062 D3 tier 2 at the producer: the four membership acts emit their evidence with the plan, the
/// price in the market's currency and the trial as the server resolved them. Both subscribe surfaces
/// share one label and one record; the web checkout has no membership row yet (the webhook provisions
/// it) so its resource id is null; a replayed native confirm resolves to the row an earlier attempt
/// created and says so with <c>reconciled = true</c> (Verification #13) — two rows, one purchase.
/// </summary>
public sealed class MembershipAuditEvidenceTests
{
    private const string UserId = "user-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string StripeCustomerId = "cus_test_1";
    private const string StripePriceId = "price_test_1";
    private const string ClientToken = "idem-token-abc";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<ICurrencyResolutionService> _currencyResolution = MarketResolution.Resolving();
    private readonly AuditContext _auditContext = new();
    private readonly MembershipPlan _plan;

    public MembershipAuditEvidenceTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        var user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        user.Id = UserId;
        user.AssignStripeCustomerId(StripeCustomerId);
        _userRepository.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        _plan = MembershipPlan.Create(
            code: PlanCode, name: "Plus Monthly", discountPercentage: 5m, freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true, billingInterval: BillingInterval.Monthly, trialPeriodDays: 14);
        _planRepository.Setup(r => r.GetByCodeAsync(PlanCode, It.IsAny<CancellationToken>())).ReturnsAsync(_plan);
        _priceRepository.PriceIn(_plan.Id, MembershipPricingMockFactory.CzkCurrencyId, StripePriceId, price: 199m);

        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
        _membershipRepository
            .Setup(r => r.HasEverStartedTrialAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _stripe
            .Setup(c => c.CreateMembershipCheckoutSessionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://checkout.stripe.com/c/pay/cs_test");
        _stripe
            .Setup(c => c.CreateSubscriptionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, int _, string attemptId, CancellationToken _) =>
                new SubscriptionResult($"sub_{attemptId}", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));
        _stripe
            .Setup(c => c.CreateSetupIntentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SetupIntentResult("seti_1", "seti_secret"));
        _stripe
            .Setup(c => c.CreateEphemeralKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ek_test");
    }

    private StripeCustomerResolver CustomerResolver() =>
        new(new Mock<IUserStripeCustomerRepository>().Object, _membershipRepository.Object, _stripe.Object,
            NullLogger<StripeCustomerResolver>.Instance);

    private CreateMembershipCheckoutSession.Handler CheckoutHandler() =>
        new(_userRepository.Object, _membershipRepository.Object, _planRepository.Object, _priceRepository.Object,
            _currencyResolution.Object, _session.Object, _stripe.Object, new StripeConfig(new ConfigurationBuilder().Build()),
            new MembershipTrialResolver(_membershipRepository.Object), CustomerResolver(), _auditContext,
            NullLogger<CreateMembershipCheckoutSession.Handler>.Instance);

    private CreateMembershipSubscription.Handler SubscribeHandler() =>
        new(_userRepository.Object, _membershipRepository.Object, _planRepository.Object, _priceRepository.Object,
            _currencyResolution.Object, _session.Object, _stripe.Object, new StripeConfig(new ConfigurationBuilder().Build()),
            new MembershipTrialResolver(_membershipRepository.Object), CustomerResolver(), _auditContext,
            NullLogger<CreateMembershipSubscription.Handler>.Instance);

    private SwapMembershipPlan.Handler SwapHandler() =>
        new(_membershipRepository.Object, _planRepository.Object, _priceRepository.Object, _session.Object,
            _stripe.Object, new StripeConfig(new ConfigurationBuilder().Build()), _auditContext,
            NullLogger<SwapMembershipPlan.Handler>.Instance);

    private CancelMembershipSubscription.Handler CancelHandler() =>
        new(_membershipRepository.Object, _session.Object, _stripe.Object, _auditContext,
            NullLogger<CancelMembershipSubscription.Handler>.Instance);

    private static JsonElement Payload(AuditSnapshot? snapshot) => JsonDocument.Parse(snapshot!.AfterJson!).RootElement;

    private static void AssertSubscribeFacts(JsonElement payload, string channel, bool reconciled, int? trialDays = 14)
    {
        Assert.Equal(PlanCode, payload.GetProperty("planCode").GetString());
        Assert.Equal("CZK", payload.GetProperty("currencyCode").GetString());
        Assert.Equal(199m, payload.GetProperty("price").GetDecimal());
        Assert.Equal(199m, payload.GetProperty("monthlyEquivalentPrice").GetDecimal());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("countryId").ValueKind);
        if (trialDays is { } days)
        {
            Assert.Equal(days, payload.GetProperty("trialDays").GetInt32());
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, payload.GetProperty("trialDays").ValueKind);
        }

        Assert.Equal(channel, payload.GetProperty("channel").GetString());
        Assert.Equal(reconciled, payload.GetProperty("reconciled").GetBoolean());
        Assert.Equal(8, payload.EnumerateObject().Count());
    }

    [Theory]
    [InlineData(typeof(CreateMembershipCheckoutSession.Command), "customer.membership.subscribe")]
    [InlineData(typeof(CreateMembershipSubscription.Command), "customer.membership.subscribe")]
    [InlineData(typeof(SwapMembershipPlan.Command), "customer.membership.swap")]
    [InlineData(typeof(CancelMembershipSubscription.Command), "customer.membership.cancel")]
    public void The_Membership_Markers_Are_Frozen_On_The_Membership(Type commandType, string expectedLabel)
    {
        var descriptor = AuditActionDescriptor.For(commandType);

        Assert.Equal(expectedLabel, descriptor.Action);
        Assert.Equal("UserMembership", descriptor.ResourceType);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.False(descriptor.AllowsAnonymousActor);
    }

    // ── subscribe: web checkout ──────────────────────────────────────────────

    [Fact]
    public async Task A_Web_Checkout_Records_The_Plan_Price_And_Trial_With_No_Membership_Id_Yet()
    {
        var result = await CheckoutHandler().Handle(new CreateMembershipCheckoutSession.Command(PlanCode), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("UserMembership", snapshot!.ResourceType);
        Assert.Null(snapshot.ResourceId);
        AssertSubscribeFacts(Payload(snapshot), channel: "checkout", reconciled: false);
    }

    [Fact]
    public async Task A_Web_Checkout_For_A_Customer_Who_Used_Their_Trial_Records_No_Trial()
    {
        _membershipRepository
            .Setup(r => r.HasEverStartedTrialAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CheckoutHandler().Handle(new CreateMembershipCheckoutSession.Command(PlanCode), CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertSubscribeFacts(Payload(_auditContext.DrainSnapshot()), channel: "checkout", reconciled: false, trialDays: null);
    }

    [Fact]
    public async Task A_Refused_Checkout_Records_No_Evidence()
    {
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembershipMockFactory.Paid(UserId));

        var result = await CheckoutHandler().Handle(new CreateMembershipCheckoutSession.Command(PlanCode), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipAlreadyActive, result.Error!.Message);
        Assert.Null(_auditContext.DrainSnapshot());
    }

    // ── subscribe: native flow ───────────────────────────────────────────────

    [Fact]
    public async Task A_Native_Subscribe_Start_Records_The_Same_Facts_With_No_Membership_Id_Yet()
    {
        var result = await SubscribeHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.Equal("UserMembership", snapshot!.ResourceType);
        Assert.Null(snapshot.ResourceId);
        AssertSubscribeFacts(Payload(snapshot), channel: "subscribe", reconciled: false);
    }

    [Fact]
    public async Task A_Confirmed_Native_Subscribe_Records_The_Membership_It_Created()
    {
        UserMembership? added = null;
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>())).Callback<UserMembership>(m => added = m);

        var result = await SubscribeHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: true) { IdempotencyToken = ClientToken },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.Equal(added!.Id, snapshot!.ResourceId);
        AssertSubscribeFacts(Payload(snapshot), channel: "subscribe", reconciled: false);
    }

    /// <summary>
    /// Verification #13: the replay guard is inside the handler and its reconcile branch returns
    /// Success, so the pipeline writes a second row. The second row must not read as a second purchase.
    /// </summary>
    [Fact]
    public async Task A_Replayed_Confirm_Creates_No_Second_Membership_And_Its_Second_Row_Says_Reconciled()
    {
        UserMembership? tracked = null;
        var added = new List<UserMembership>();
        _membershipRepository
            .Setup(r => r.GetByStripeSubscriptionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => tracked);
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(m => { added.Add(m); tracked = m; });
        var command = new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: true) { IdempotencyToken = ClientToken };

        var first = await SubscribeHandler().Handle(command, CancellationToken.None);
        var firstSnapshot = _auditContext.DrainSnapshot();
        var second = await SubscribeHandler().Handle(command, CancellationToken.None);
        var secondSnapshot = _auditContext.DrainSnapshot();

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Single(added);
        Assert.Equal(added[0].Id, firstSnapshot!.ResourceId);
        Assert.Equal(added[0].Id, secondSnapshot!.ResourceId);
        Assert.False(Payload(firstSnapshot).GetProperty("reconciled").GetBoolean());
        AssertSubscribeFacts(Payload(secondSnapshot), channel: "subscribe", reconciled: true);
    }

    // ── swap ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Swap_Records_Both_Plans_With_Their_Prices_In_The_Subscriptions_Currency()
    {
        var membership = UserMembershipMockFactory.Paid(UserId, _plan.Id);
        typeof(UserMembership).GetProperty(nameof(UserMembership.MembershipPlan))!
            .GetSetMethod(nonPublic: true)!.Invoke(membership, [_plan]);
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        var yearly = MembershipPlan.Create("PLUS_YEARLY", "Plus Yearly", 5m, 4, true, BillingInterval.Yearly);
        _planRepository.Setup(r => r.GetByCodeAsync("PLUS_YEARLY", It.IsAny<CancellationToken>())).ReturnsAsync(yearly);
        _priceRepository.PriceIn(yearly.Id, MembershipPricingMockFactory.CzkCurrencyId, "price_yearly", price: 1990m);
        var periodStart = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        _stripe
            .Setup(c => c.SwapSubscriptionPriceAsync(It.IsAny<string>(), "price_yearly", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionResult("sub_swapped", periodStart, periodStart.AddYears(1)));

        var result = await SwapHandler().Handle(new SwapMembershipPlan.Command("PLUS_YEARLY"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.Equal(membership.Id, snapshot!.ResourceId);
        var payload = Payload(snapshot);
        Assert.Equal(PlanCode, payload.GetProperty("before").GetProperty("planCode").GetString());
        Assert.Equal(199m, payload.GetProperty("before").GetProperty("price").GetDecimal());
        Assert.Equal("PLUS_YEARLY", payload.GetProperty("after").GetProperty("planCode").GetString());
        Assert.Equal(1990m, payload.GetProperty("after").GetProperty("price").GetDecimal());
        Assert.Equal("CZK", payload.GetProperty("currencyCode").GetString());
        Assert.Equal(periodStart, payload.GetProperty("effectiveAt").GetDateTimeOffset().UtcDateTime);
    }

    // ── cancel ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Cancellation_Records_The_Plan_And_When_The_Benefits_Run_Out()
    {
        var membership = UserMembershipMockFactory.Paid(UserId);
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var result = await CancelHandler().Handle(new CancelMembershipSubscription.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.Equal(membership.Id, snapshot!.ResourceId);
        var payload = Payload(snapshot);
        Assert.Equal(PlanCode, payload.GetProperty("planCode").GetString());
        Assert.Equal(
            DateTime.SpecifyKind(membership.CurrentPeriodEnd, DateTimeKind.Utc),
            payload.GetProperty("currentPeriodEndsAt").GetDateTimeOffset().UtcDateTime);
        Assert.Equal(2, payload.EnumerateObject().Count());
    }

    [Fact]
    public async Task A_Cancellation_With_No_Membership_Is_Refused_And_Records_No_Evidence()
    {
        var result = await CancelHandler().Handle(new CancelMembershipSubscription.Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipNotFound, result.Error!.Message);
        Assert.Null(_auditContext.DrainSnapshot());
    }
}
