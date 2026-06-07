using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// Phase-2 confirmed-subscribe must be safe to run twice. When Stripe succeeded but the local
/// <see cref="UserMembership"/> commit failed, a retried confirm carries the same deterministic
/// idempotency key, so Stripe replays the same subscription rather than billing a second one. The
/// handler must reconcile to the already-tracked subscription instead of creating a second local row.
/// Stripe is modelled so a given attempt id always yields the same SubscriptionId; the repository is
/// modelled so the subscription is already tracked locally.
/// </summary>
public class CreateMembershipSubscriptionReconcileOnRetryTests
{
    private const string UserId = "user-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string StripeCustomerId = "cus_test_1";
    private const string StripePriceId = "price_test_1";
    private const string ClientToken = "idem-token-abc";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();

    private readonly MembershipPlan _plan;

    public CreateMembershipSubscriptionReconcileOnRetryTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        var user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        user.Id = UserId;
        user.AssignStripeCustomerId(StripeCustomerId);
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

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

        _stripe
            .Setup(c => c.CreateSubscriptionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, int _, string attemptId, CancellationToken _) =>
                new SubscriptionResult(
                    SubscriptionId: $"sub_{attemptId}",
                    CurrentPeriodStart: DateTime.UtcNow,
                    CurrentPeriodEnd: DateTime.UtcNow.AddMonths(1)));
    }

    private CreateMembershipSubscription.Handler CreateHandler() =>
        new(
            _userRepository.Object,
            _membershipRepository.Object,
            _planRepository.Object,
            _session.Object,
            _stripe.Object,
            NullLogger<CreateMembershipSubscription.Handler>.Instance);

    private static CreateMembershipSubscription.Command ConfirmedCommand() =>
        new(PlanCode, PaymentMethodConfirmed: true) { IdempotencyToken = ClientToken };

    [Fact]
    public async Task Phase2Retry_AfterCommitFail_StripeReplays_ReconcilesToExistingSubscription_NoSecondRow()
    {
        // The active-membership guard never trips: the failed attempt left no committed active row, so the
        // retry proceeds past every GetActiveForUserAsync to the Stripe call again.
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var added = new List<UserMembership>();
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(added.Add);

        // The subscription is already tracked locally for this Stripe subscription id (deterministic key →
        // Stripe replay → same id). The reconcile guard must resolve to it instead of adding a duplicate.
        var alreadyTracked = UserMembership.Create(
            userId: UserId,
            membershipPlanId: _plan.Id,
            stripeSubscriptionId: $"sub_tok-{ClientToken}",
            currentPeriodStart: DateTime.UtcNow,
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
        _membershipRepository
            .Setup(r => r.GetByStripeSubscriptionIdAsync(
                $"sub_tok-{ClientToken}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(alreadyTracked);

        var result = await CreateHandler().Handle(ConfirmedCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(alreadyTracked.Id, result.Value.MembershipId);
        Assert.Empty(added);
        _membershipRepository.Verify(r => r.Add(It.IsAny<UserMembership>()), Times.Never);
    }

    [Fact]
    public async Task Phase2_TwiceAcrossAttempts_SubscriptionEffectFiresOnce()
    {
        UserMembership? tracked = null;
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
        _membershipRepository
            .Setup(r => r.GetByStripeSubscriptionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => tracked);

        var added = new List<UserMembership>();
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(m =>
            {
                added.Add(m);
                tracked = m;
            });

        var first = await CreateHandler().Handle(ConfirmedCommand(), CancellationToken.None);
        var second = await CreateHandler().Handle(ConfirmedCommand(), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Single(added);
        Assert.Single(added.Select(m => m.StripeSubscriptionId).Distinct());
        Assert.Equal(added[0].Id, second.Value.MembershipId);
    }
}
