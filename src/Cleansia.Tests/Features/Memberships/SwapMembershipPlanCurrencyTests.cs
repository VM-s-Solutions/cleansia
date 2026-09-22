using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// A subscription keeps its currency for life, so a swap hands Stripe the target plan's row in THAT
/// currency, whatever market the customer is browsing — and refuses when there is none.
/// </summary>
public class SwapMembershipPlanCurrencyTests
{
    private const string UserId = "user-1";
    private const string SubscriptionId = "sub_1";

    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly MembershipPlan _yearly;

    public SwapMembershipPlanCurrencyTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        var monthly = MembershipPlan.Create("PLUS_MONTHLY", "Monthly", 5m, 4, true);
        monthly.Id = "plan-monthly";
        _yearly = MembershipPlan.Create("PLUS_YEARLY", "Yearly", 5m, 4, true, BillingInterval.Yearly);
        _yearly.Id = "plan-yearly";
        _planRepository
            .Setup(r => r.GetByCodeAsync("PLUS_YEARLY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_yearly);

        var membership = UserMembership.Create(
            UserId, monthly.Id, MembershipPricingMockFactory.CzkCurrencyId, SubscriptionId,
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(20));
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        _stripe
            .Setup(c => c.SwapSubscriptionPriceAsync(SubscriptionId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionResult(SubscriptionId, DateTime.UtcNow, DateTime.UtcNow.AddYears(1)));
    }

    private SwapMembershipPlan.Handler Handler() =>
        new(
            _membershipRepository.Object,
            _planRepository.Object,
            _priceRepository.Object,
            _session.Object,
            _stripe.Object,
            new StripeConfig(new ConfigurationBuilder().Build()),
            new AuditContext(),
            NullLogger<SwapMembershipPlan.Handler>.Instance);

    [Theory]
    [InlineData("active", false, true)]
    [InlineData("active", true, false)]
    [InlineData("trialing", true, false)]
    [InlineData("past_due", false, false)]
    [InlineData("incomplete", false, false)]
    public async Task Only_a_paid_swap_response_rearms_the_lapse(string status, bool trial, bool paid)
    {
        var now = DateTime.UtcNow;
        var membership = UserMembership.Create(UserId, "plan-monthly", MembershipPricingMockFactory.CzkCurrencyId,
            SubscriptionId, now.AddMonths(-1), now.AddHours(-1));
        membership.RecordRecurringPauseState("active", now.AddMonths(-1), now.AddMonths(-1));
        Assert.True(membership.TryMarkRecurringPauseNotificationSent(now.AddMinutes(-1)));
        _membershipRepository.Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        _priceRepository.PriceIn(_yearly.Id, MembershipPricingMockFactory.CzkCurrencyId, "price_yearly_czk", 2030m);
        _stripe.Setup(c => c.SwapSubscriptionPriceAsync(SubscriptionId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionResult(SubscriptionId, now, now.AddYears(1), trial ? now.AddDays(7) : null, status));

        var result = await Handler().Handle(new SwapMembershipPlan.Command("PLUS_YEARLY"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(paid, membership.RecurringPauseNotificationSentAt is null);
        Assert.Equal(paid, membership.PaidPeriodConfirmedAt > now.AddMinutes(-1));
        Assert.Equal(1, membership.RecurringPauseNotificationSequence);
        Assert.Equal(MembershipStatus.Active, membership.Status);
    }

    [Fact]
    public async Task TheTargetPlansRow_InTheMembershipsCurrency_IsHandedToStripe()
    {
        _priceRepository.PriceIn(_yearly.Id, MembershipPricingMockFactory.CzkCurrencyId, "price_yearly_czk", 2030m);
        _priceRepository.PriceIn(_yearly.Id, MembershipPricingMockFactory.EurCurrencyId, "price_yearly_eur", 59.88m);

        var result = await Handler().Handle(new SwapMembershipPlan.Command("PLUS_YEARLY"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _stripe.Verify(c => c.SwapSubscriptionPriceAsync(
            SubscriptionId, "price_yearly_czk", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ATargetPlanUnpricedInTheMembershipsCurrency_IsRefused_AndStripeIsNotCalled()
    {
        _priceRepository.PriceIn(_yearly.Id, MembershipPricingMockFactory.EurCurrencyId, "price_yearly_eur", 59.88m);

        var result = await Handler().Handle(new SwapMembershipPlan.Command("PLUS_YEARLY"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipPlanNotPricedInCurrency, result.Error!.Message);
        _stripe.Verify(c => c.SwapSubscriptionPriceAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
