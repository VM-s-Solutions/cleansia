using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using AppConstants = Cleansia.Core.AppServices.Common.Constants;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// <see cref="UserMembership.TrialEndsAtUtc"/> is both the "withhold metered benefits" deadline
/// (ADR-0035 AM-18) and — read across the user's whole membership history — the once-per-customer trial
/// marker (owner ruling 2026-08-03). These pin the webhook's side of both.
///
/// <para><b>The dunning trap ADR-0035 AM-18 names.</b> The <c>invoice.payment_failed</c> branch of
/// <c>ExtractSubscriptionShape</c> carries an Invoice, not a Subscription, so it has no period bounds and
/// no <c>trial_end</c> to report. The period bounds are already passed through unchanged; <c>trial_end</c>
/// has to get the same treatment, or a single failed payment erases the marker for exactly the customer
/// the ruling is about — re-arming both the express waivers and a second free trial.</para>
/// </summary>
public class MembershipTrialMarkerWebhookTests
{
    private const string UserId = "user-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string PlanId = "plan-1";
    private const string SubscriptionId = "sub_1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    public MembershipTrialMarkerWebhookTests()
    {
        var user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        user.Id = UserId;
        _userRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var plan = MembershipPlan.Create(
            code: PlanCode,
            name: "Plus Monthly",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_test_1",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 30);
        plan.Id = PlanId;
        _planRepository
            .Setup(r => r.GetByCodeAsync(PlanCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
    }

    private StripeSubscriptionWebhookHandler CreateHandler() =>
        new(
            _userRepository.Object,
            _membershipRepository.Object,
            _planRepository.Object,
            _tenantProvider.Object,
            NullLogger<StripeSubscriptionWebhookHandler>.Instance);

    private UserMembership ExistingTrialingMembership(DateTime trialEndsAtUtc)
    {
        var membership = UserMembership.Create(
            userId: UserId,
            membershipPlanId: PlanId,
            stripeSubscriptionId: SubscriptionId,
            currentPeriodStart: DateTime.UtcNow.AddDays(-1),
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1),
            trialEndsAtUtc: trialEndsAtUtc);
        _membershipRepository
            .Setup(r => r.GetByStripeSubscriptionIdAsync(SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        return membership;
    }

    private static Event InvoicePaymentFailedEvent() => new()
    {
        Id = "evt_invoice_failed",
        Type = AppConstants.StripeEventType.InvoicePaymentFailed,
        Data = new EventData
        {
            Object = new Invoice
            {
                Id = "in_1",
                Parent = new InvoiceParent
                {
                    SubscriptionDetails = new InvoiceParentSubscriptionDetails
                    {
                        SubscriptionId = SubscriptionId,
                    },
                },
            },
        },
    };

    private static Event SubscriptionEvent(string type, string status, DateTime? trialEnd) => new()
    {
        Id = $"evt_{type}",
        Type = type,
        Data = new EventData
        {
            Object = new Subscription
            {
                Id = SubscriptionId,
                Status = status,
                TrialEnd = trialEnd,
                Metadata = new Dictionary<string, string>
                {
                    ["UserId"] = UserId,
                    ["MembershipPlanCode"] = PlanCode,
                },
                Items = new StripeList<SubscriptionItem>
                {
                    Data =
                    [
                        new SubscriptionItem
                        {
                            CurrentPeriodStart = DateTime.UtcNow,
                            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
                        },
                    ],
                },
            },
        },
    };

    [Fact]
    public async Task InvoicePaymentFailed_DoesNotClearTheTrialMarker()
    {
        var trialEnd = DateTime.UtcNow.AddDays(20);
        var membership = ExistingTrialingMembership(trialEnd);

        await CreateHandler().HandleAsync(InvoicePaymentFailedEvent(), CancellationToken.None);

        Assert.Equal(trialEnd, membership.TrialEndsAtUtc);
        Assert.Equal(MembershipStatus.PastDue, membership.Status);
    }

    [Fact]
    public async Task InvoicePaymentFailed_LeavesThePeriodBoundsAlone()
    {
        var membership = ExistingTrialingMembership(DateTime.UtcNow.AddDays(20));
        var start = membership.CurrentPeriodStart;
        var end = membership.CurrentPeriodEnd;

        await CreateHandler().HandleAsync(InvoicePaymentFailedEvent(), CancellationToken.None);

        Assert.Equal(start, membership.CurrentPeriodStart);
        Assert.Equal(end, membership.CurrentPeriodEnd);
    }

    [Fact]
    public async Task SubscriptionUpdated_WithoutATrial_DoesNotClearAnExistingMarker()
    {
        var trialEnd = DateTime.UtcNow.AddDays(-5);
        var membership = ExistingTrialingMembership(trialEnd);

        await CreateHandler().HandleAsync(
            SubscriptionEvent(AppConstants.StripeEventType.SubscriptionUpdated, "active", trialEnd: null),
            CancellationToken.None);

        Assert.Equal(trialEnd, membership.TrialEndsAtUtc);
    }

    [Fact]
    public async Task SubscriptionUpdated_CarriesAMovedTrialEndThrough()
    {
        var membership = ExistingTrialingMembership(DateTime.UtcNow.AddDays(20));
        var extended = DateTime.UtcNow.AddDays(40);

        await CreateHandler().HandleAsync(
            SubscriptionEvent(AppConstants.StripeEventType.SubscriptionUpdated, "trialing", extended),
            CancellationToken.None);

        Assert.Equal(extended, membership.TrialEndsAtUtc);
    }

    /// <summary>
    /// The web Checkout flow's only creator of the local row. A trial granted here and not mirrored is a
    /// trial the once-per-customer rule can never see.
    /// </summary>
    [Fact]
    public async Task SubscriptionCreated_MirrorsStripesTrialEndOntoTheNewRow()
    {
        var trialEnd = DateTime.UtcNow.AddDays(30);
        _membershipRepository
            .Setup(r => r.GetByStripeSubscriptionIdAsync(SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        UserMembership? added = null;
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(m => added = m);

        await CreateHandler().HandleAsync(
            SubscriptionEvent(AppConstants.StripeEventType.SubscriptionCreated, "trialing", trialEnd),
            CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(trialEnd, added!.TrialEndsAtUtc);
        Assert.True(added.IsInTrial);
    }

    [Fact]
    public async Task SubscriptionCreated_WithoutATrial_LeavesTheMarkerNull()
    {
        _membershipRepository
            .Setup(r => r.GetByStripeSubscriptionIdAsync(SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        UserMembership? added = null;
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(m => added = m);

        await CreateHandler().HandleAsync(
            SubscriptionEvent(AppConstants.StripeEventType.SubscriptionCreated, "active", trialEnd: null),
            CancellationToken.None);

        Assert.NotNull(added);
        Assert.Null(added!.TrialEndsAtUtc);
    }
}
