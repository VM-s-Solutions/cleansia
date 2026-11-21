using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using AppConstants = Cleansia.Core.AppServices.Common.Constants;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// The subscription webhooks are the backstop of a Plus subscribe: a subscription carrying the UserId and
/// MembershipPlanCode metadata (web Checkout and phone alike) is granted its membership by whichever of its
/// created or updated events first says it is paid or trialing, so a phone whose app never confirms again
/// after a 3-D Secure step still ends with its one membership. An incomplete, expired or cancelled subscription
/// is granted nothing, and a late incomplete event does not end a membership already granted.
/// </summary>
public class SubscriptionWebhookProvisionsOnlyLiveTests
{
    private const string UserId = "user-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string SubscriptionId = "sub_phone_1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly List<UserMembership> _rows = [];

    public SubscriptionWebhookProvisionsOnlyLiveTests()
    {
        var user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        user.Id = UserId;
        user.TenantId = "tenant-A";
        _userRepository.Setup(r => r.GetByIdIgnoringTenantAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var plan = MembershipPlan.Create(
            code: PlanCode,
            name: "Plus Monthly",
            discountPercentage: 5m,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 0);
        _planRepository.Setup(r => r.GetByCodeAsync(PlanCode, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        _currencyRepository.Setup(r => r.GetByCodeAsync("CZK", It.IsAny<CancellationToken>())).ReturnsAsync(MembershipPricingMockFactory.Czk());

        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>())).Callback<UserMembership>(_rows.Add);
        _membershipRepository
            .Setup(r => r.GetByStripeSubscriptionIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => _rows.SingleOrDefault(m => m.StripeSubscriptionId == id));
        _membershipRepository
            .Setup(r => r.GetLifecycleForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _rows.SingleOrDefault(m => m.Status == MembershipStatus.Active));
    }

    private Task Deliver(string type, string status) =>
        new StripeSubscriptionWebhookHandler(
                _userRepository.Object,
                _membershipRepository.Object,
                _planRepository.Object,
                _currencyRepository.Object,
                Mock.Of<ITenantProvider>(),
                Mock.Of<INotificationProducer>(),
                NullLogger<StripeSubscriptionWebhookHandler>.Instance)
            .HandleAsync(
                new Event
                {
                    Id = $"evt_{Guid.NewGuid():N}",
                    Type = type,
                    Created = DateTime.UtcNow,
                    Data = new EventData
                    {
                        Object = new Subscription
                        {
                            Id = SubscriptionId,
                            Status = status,
                            Currency = "czk",
                            Metadata = new Dictionary<string, string> { ["UserId"] = UserId, ["MembershipPlanCode"] = PlanCode },
                            Items = new StripeList<SubscriptionItem>
                            {
                                Data = [new SubscriptionItem { CurrentPeriodStart = DateTime.UtcNow, CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1) }],
                            },
                        },
                    },
                },
                CancellationToken.None);

    [Theory]
    [InlineData(AppConstants.StripeEventType.SubscriptionCreated, "active")]
    [InlineData(AppConstants.StripeEventType.SubscriptionCreated, "trialing")]
    [InlineData(AppConstants.StripeEventType.SubscriptionUpdated, "active")]
    [InlineData(AppConstants.StripeEventType.SubscriptionUpdated, "trialing")]
    public async Task A_paid_or_trialing_subscription_the_platform_does_not_know_yet_is_granted_one_membership(string type, string status)
    {
        await Deliver(type, status);

        var membership = Assert.Single(_rows);
        Assert.Equal((SubscriptionId, MembershipStatus.Active), (membership.StripeSubscriptionId, membership.Status));
    }

    [Theory]
    [InlineData(AppConstants.StripeEventType.SubscriptionCreated, "incomplete")]
    [InlineData(AppConstants.StripeEventType.SubscriptionCreated, "incomplete_expired")]
    [InlineData(AppConstants.StripeEventType.SubscriptionUpdated, "incomplete_expired")]
    [InlineData(AppConstants.StripeEventType.SubscriptionUpdated, "canceled")]
    [InlineData(AppConstants.StripeEventType.SubscriptionUpdated, "past_due")]
    [InlineData(AppConstants.StripeEventType.SubscriptionDeleted, "canceled")]
    public async Task A_subscription_that_is_not_paid_is_granted_nothing(string type, string status)
    {
        await Deliver(type, status);

        Assert.Empty(_rows);
    }

    [Fact]
    public async Task A_3ds_subscription_created_incomplete_gets_no_row_until_it_is_paid_and_then_exactly_one()
    {
        await Deliver(AppConstants.StripeEventType.SubscriptionCreated, "incomplete");
        Assert.Empty(_rows);
        await Deliver(AppConstants.StripeEventType.SubscriptionUpdated, "active");
        await Deliver(AppConstants.StripeEventType.SubscriptionUpdated, "active");

        var membership = Assert.Single(_rows);
        Assert.Equal(MembershipStatus.Active, membership.Status);
    }

    [Fact]
    public async Task A_late_incomplete_created_event_does_not_end_the_membership_its_payment_granted()
    {
        await Deliver(AppConstants.StripeEventType.SubscriptionUpdated, "active");
        await Deliver(AppConstants.StripeEventType.SubscriptionCreated, "incomplete");

        var membership = Assert.Single(_rows);
        Assert.Equal(MembershipStatus.Active, membership.Status);
    }
}
