using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using AppConstants = Cleansia.Core.AppServices.Common.Constants;
using IStripeClient = Cleansia.Core.Clients.Abstractions.Stripe.IStripeClient;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// A Plus renewal that fails leaves the member past due: benefits stop at once, Stripe keeps retrying the
/// card, and the subscription is still alive. Owner ruling 2026-09-28: the member is told, may cancel
/// with immediate effect — Stripe cancels now and the open invoice is voided, so nothing more is charged
/// — and a paid-up member keeps cancelling at the end of the period they paid for.
/// </summary>
public sealed class PastDueMembershipTests
{
    private const string UserId = "user-past-due";
    private const string SubscriptionId = "sub_past_due";

    private readonly Mock<IUserMembershipRepository> _memberships = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<INotificationProducer> _notifications = new();

    public PastDueMembershipTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
    }

    [Fact]
    public async Task A_Past_Due_Member_Cancels_Now_And_Nothing_More_Is_Charged()
    {
        var membership = ArrangeLive("past_due");

        var result = await CancelHandler().Handle(new CancelMembershipSubscription.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _stripe.Verify(c => c.CancelSubscriptionNowAsync(SubscriptionId, It.IsAny<CancellationToken>()), Times.Once);
        _stripe.Verify(c => c.CancelSubscriptionAtPeriodEndAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(MembershipStatus.Cancelled, membership.Status);
        Assert.NotNull(membership.CancelledAt);
        Assert.True(result.Value!.EffectiveEndDate < membership.CurrentPeriodEnd);
        Assert.True(result.Value.EffectiveEndDate <= DateTime.UtcNow);
    }

    [Fact]
    public async Task A_Paid_Up_Member_Still_Cancels_At_The_End_Of_The_Period()
    {
        var membership = ArrangeLive("active");

        var result = await CancelHandler().Handle(new CancelMembershipSubscription.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _stripe.Verify(c => c.CancelSubscriptionAtPeriodEndAsync(SubscriptionId, It.IsAny<CancellationToken>()), Times.Once);
        _stripe.Verify(c => c.CancelSubscriptionNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal(membership.CurrentPeriodEnd, result.Value!.EffectiveEndDate);
    }

    /// <summary>
    /// A plan swap invoices the difference at once; on a card that is already failing it would only add a
    /// second unpaid invoice. A past-due member settles or cancels first, as before.
    /// </summary>
    [Fact]
    public async Task A_Past_Due_Member_Cannot_Swap_Plans()
    {
        ArrangeLive("past_due");
        var stripeConfig = new Mock<IStripeConfig>();
        stripeConfig.Setup(c => c.Enabled).Returns(true);

        var result = await new SwapMembershipPlan.Handler(
                _memberships.Object,
                Mock.Of<IMembershipPlanRepository>(),
                Mock.Of<IMembershipPlanPriceRepository>(),
                _session.Object,
                _stripe.Object,
                stripeConfig.Object,
                new AuditContext(),
                NullLogger<SwapMembershipPlan.Handler>.Instance)
            .Handle(new SwapMembershipPlan.Command("PLUS_YEARLY"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipNotFound, result.Error!.Message);
        _stripe.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Failed_Renewal_Tells_The_Member()
    {
        var membership = ArrangeLive("active");
        _memberships
            .Setup(r => r.GetByStripeSubscriptionIdAsync(SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        await WebhookHandler().HandleAsync(PaymentFailedEvent("evt_failed_1"), CancellationToken.None);

        Assert.Equal(MembershipStatus.PastDue, membership.Status);
        _notifications.Verify(p => p.NotifyAsync(
            UserId,
            NotificationEventCatalog.MembershipPaymentFailed,
            It.Is<Dictionary<string, string>>(a => a["membershipId"] == membership.Id),
            membership.TenantId,
            "evt_failed_1",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Stripe retries a delivery it could not make for days, so a renewal failure can arrive after the
    /// member already cancelled. It must not revive the enrolment, which would then block every new
    /// subscription, and must not tell them about a card they no longer pay with.
    /// </summary>
    [Fact]
    public async Task A_Late_Payment_Failure_Does_Not_Revive_A_Cancelled_Membership()
    {
        var membership = ArrangeLive("past_due");
        await CancelHandler().Handle(new CancelMembershipSubscription.Command(), CancellationToken.None);
        _memberships
            .Setup(r => r.GetByStripeSubscriptionIdAsync(SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        await WebhookHandler().HandleAsync(PaymentFailedEvent("evt_failed_late"), CancellationToken.None);

        Assert.Equal(MembershipStatus.Cancelled, membership.Status);
        _notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Subscription_Update_Tells_Nobody()
    {
        var membership = ArrangeLive("active");
        _memberships
            .Setup(r => r.GetByStripeSubscriptionIdAsync(SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        await WebhookHandler().HandleAsync(new Event
        {
            Id = "evt_updated_1",
            Type = AppConstants.StripeEventType.SubscriptionUpdated,
            Data = new EventData
            {
                Object = new Subscription
                {
                    Id = SubscriptionId,
                    Status = "past_due",
                    Items = new StripeList<SubscriptionItem>
                    {
                        Data =
                        [
                            new SubscriptionItem
                            {
                                CurrentPeriodStart = membership.CurrentPeriodStart,
                                CurrentPeriodEnd = membership.CurrentPeriodEnd,
                            },
                        ],
                    },
                },
            },
        }, CancellationToken.None);

        _notifications.VerifyNoOtherCalls();
    }

    private UserMembership ArrangeLive(string stripeStatus)
    {
        var plan = MembershipPlan.Create(
            code: "PLUS_MONTHLY",
            name: "Plus Monthly",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true);
        var membership = UserMembership.Create(
            userId: UserId,
            membershipPlanId: plan.Id,
            currencyId: "currency-czk",
            stripeSubscriptionId: SubscriptionId,
            currentPeriodStart: DateTime.UtcNow.AddDays(-3),
            currentPeriodEnd: DateTime.UtcNow.AddDays(27));
        membership.UpdateFromStripeWebhook(stripeStatus, membership.CurrentPeriodStart, membership.CurrentPeriodEnd, trialEndsAtUtc: null);
        _memberships
            .Setup(r => r.GetLifecycleForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        return membership;
    }

    private CancelMembershipSubscription.Handler CancelHandler() =>
        new(
            _memberships.Object,
            _session.Object,
            _stripe.Object,
            new AuditContext(),
            NullLogger<CancelMembershipSubscription.Handler>.Instance);

    private StripeSubscriptionWebhookHandler WebhookHandler() =>
        new(
            Mock.Of<IUserRepository>(),
            _memberships.Object,
            Mock.Of<IMembershipPlanRepository>(),
            Mock.Of<ICurrencyRepository>(),
            Mock.Of<ITenantProvider>(),
            _notifications.Object,
            NullLogger<StripeSubscriptionWebhookHandler>.Instance);

    private static Event PaymentFailedEvent(string eventId) =>
        new()
        {
            Id = eventId,
            Type = AppConstants.StripeEventType.InvoicePaymentFailed,
            Data = new EventData
            {
                Object = new Invoice
                {
                    Id = "in_failed",
                    Parent = new InvoiceParent
                    {
                        SubscriptionDetails = new InvoiceParentSubscriptionDetails { SubscriptionId = SubscriptionId },
                    },
                },
            },
        };
}
