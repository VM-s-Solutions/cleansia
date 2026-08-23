using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// ADR-0035 D6 — the pure resolver's decision table.
///
/// <para>Includes <c>TC-BENEFIT-GATE-0</c>'s plan legs (the flag off, a zero quota, a guest) and
/// <c>TC-BENEFIT-TRIAL-0</c> (owner ruling 2026-08-03: no metered waivers during the 14-day trial).
/// The <c>PastDue</c> leg lives in <c>ExpressWaiverMembershipPredicateTests</c> because it is a property
/// of the shared membership predicate, not of this class — asserting it against a mock here would prove
/// only that the mock returned null.</para>
/// </summary>
public class ExpressWaiverResolverTests
{
    private const string UserId = "user-waiver-1";
    private const string PeriodKey = "C:2026-08";
    private static readonly DateTime NowUtc = new(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);

    // 3 hours out — inside BookingPolicy's [2h, 4h) express window.
    private static readonly DateTime ExpressCleaningUtc = NowUtc.AddHours(3);

    private readonly Mock<IUserMembershipRepository> _memberships = new();
    private readonly Mock<IMembershipBenefitUsageRepository> _usage = new();
    private readonly Mock<IBenefitPeriodKeyFactory> _periodKeys = new();

    public ExpressWaiverResolverTests()
    {
        _periodKeys
            .Setup(f => f.GetCalendarKeyAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PeriodKey);
        _usage
            .Setup(u => u.CountLiveInPeriodAsync(
                It.IsAny<string>(), It.IsAny<MembershipBenefitKind>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private ExpressWaiverResolver CreateResolver()
        => new(_memberships.Object, _usage.Object, _periodKeys.Object);

    private void ArrangeMembership(
        bool allowsExpressUpgrade = true,
        int expressUpgradesPerMonth = 2,
        DateTime? trialEndsAtUtc = null)
    {
        var plan = MembershipPlan.Create(
            code: "PLUS_MONTHLY",
            name: "Plus",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_plus",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: allowsExpressUpgrade);
        plan.UpdateBenefits(5m, 4, allowsExpressUpgrade, expressUpgradesPerMonth);

        var membership = UserMembership.Create(
            UserId, plan.Id, "sub_test", NowUtc.AddDays(-10), NowUtc.AddDays(20), trialEndsAtUtc);
        typeof(UserMembership).GetProperty(nameof(UserMembership.MembershipPlan))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(membership, [plan]);

        _memberships
            .Setup(r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
    }

    [Fact]
    public async Task ExpressSlot_WithQuotaLeft_IsWaived()
    {
        ArrangeMembership();

        var waiver = await CreateResolver()
            .ResolveForUserAsync(UserId, ExpressCleaningUtc, NowUtc, CancellationToken.None);

        Assert.True(waiver.InExpressWindow);
        Assert.True(waiver.Waived);
        Assert.Equal(2, waiver.Quota);
        Assert.Equal(2, waiver.RemainingBeforeThisBooking);
        Assert.Equal(PeriodKey, waiver.PeriodKey);
    }

    [Fact]
    public async Task NonExpressSlot_ConsumesNothingAndIsNotWaived()
    {
        ArrangeMembership();

        var waiver = await CreateResolver()
            .ResolveForUserAsync(UserId, NowUtc.AddHours(9), NowUtc, CancellationToken.None);

        Assert.False(waiver.InExpressWindow);
        Assert.False(waiver.Waived);
        // The balance is still reported: a 9-hour lead is free for everybody, so nothing is spent.
        Assert.Equal(2, waiver.RemainingBeforeThisBooking);
    }

    [Fact]
    public async Task QuotaExhausted_IsNotWaivedButStillReportsTheWindow()
    {
        ArrangeMembership();
        _usage
            .Setup(u => u.CountLiveInPeriodAsync(
                UserId, MembershipBenefitKind.ExpressUpgrade, PeriodKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var waiver = await CreateResolver()
            .ResolveForUserAsync(UserId, ExpressCleaningUtc, NowUtc, CancellationToken.None);

        Assert.True(waiver.InExpressWindow);
        Assert.False(waiver.Waived);
        Assert.Equal(0, waiver.RemainingBeforeThisBooking);
    }

    /// <summary>
    /// The E-3 downgrade case on the READ side: the quota comes from the CURRENT plan while the count is
    /// over the period, so a mid-month downgrade with more already consumed clamps to zero rather than
    /// going negative — and the already-granted waivers are not clawed back.
    /// </summary>
    [Fact]
    public async Task QuotaSmallerThanConsumed_ClampsToZero()
    {
        ArrangeMembership(expressUpgradesPerMonth: 2);
        _usage
            .Setup(u => u.CountLiveInPeriodAsync(
                UserId, MembershipBenefitKind.ExpressUpgrade, PeriodKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var waiver = await CreateResolver()
            .ResolveForUserAsync(UserId, ExpressCleaningUtc, NowUtc, CancellationToken.None);

        Assert.Equal(0, waiver.RemainingBeforeThisBooking);
        Assert.False(waiver.Waived);
    }

    [Fact]
    public async Task GuestBooking_ShortCircuitsWithoutReadingAnything()
    {
        var waiver = await CreateResolver()
            .ResolveForUserAsync(null, ExpressCleaningUtc, NowUtc, CancellationToken.None);

        Assert.False(waiver.Waived);
        Assert.Equal(0, waiver.Quota);
        _memberships.Verify(
            r => r.GetActiveForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _usage.Verify(
            u => u.CountLiveInPeriodAsync(
                It.IsAny<string>(), It.IsAny<MembershipBenefitKind>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PlanWithoutTheFlag_WaivesNothing()
    {
        ArrangeMembership(allowsExpressUpgrade: false, expressUpgradesPerMonth: 2);

        var waiver = await CreateResolver()
            .ResolveForUserAsync(UserId, ExpressCleaningUtc, NowUtc, CancellationToken.None);

        Assert.False(waiver.Waived);
        Assert.Equal(0, waiver.Quota);
    }

    /// <summary>Fail-closed: the seeded default is 0, and 0 never secretly means "unlimited".</summary>
    [Fact]
    public async Task PlanWithZeroQuota_WaivesNothing()
    {
        ArrangeMembership(allowsExpressUpgrade: true, expressUpgradesPerMonth: 0);

        var waiver = await CreateResolver()
            .ResolveForUserAsync(UserId, ExpressCleaningUtc, NowUtc, CancellationToken.None);

        Assert.False(waiver.Waived);
        Assert.Equal(0, waiver.Quota);
    }

    /// <summary>
    /// Owner ruling 2026-08-03: metered waivers begin at first payment. Without this a customer who
    /// subscribes on the 28th draws four waivers for 0 Kč across two calendar keys and cancels before the
    /// trial converts.
    /// </summary>
    [Fact]
    public async Task TrialingMember_GetsNoWaiver()
    {
        ArrangeMembership(trialEndsAtUtc: NowUtc.AddDays(5));

        var waiver = await CreateResolver()
            .ResolveForUserAsync(UserId, ExpressCleaningUtc, NowUtc, CancellationToken.None);

        Assert.False(waiver.Waived);
        Assert.Equal(0, waiver.RemainingBeforeThisBooking);
        // The quota is still reported so the client can say when waivers start rather than rendering a
        // bare zero that reads as "you used them up".
        Assert.Equal(2, waiver.Quota);
        _usage.Verify(
            u => u.CountLiveInPeriodAsync(
                It.IsAny<string>(), It.IsAny<MembershipBenefitKind>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MemberWhoseTrialHasEnded_GetsTheWaiver()
    {
        // NowUtc, not DateTime.UtcNow: every other instant in this class is derived from the fixed
        // clock, and mixing the wall clock in is what let the sibling case above rot silently.
        ArrangeMembership(trialEndsAtUtc: NowUtc.AddDays(-1));

        var waiver = await CreateResolver()
            .ResolveForUserAsync(UserId, ExpressCleaningUtc, NowUtc, CancellationToken.None);

        Assert.True(waiver.Waived);
    }

    /// <summary>
    /// The counting key is <c>(TenantId, UserId, BenefitKind, PeriodKey)</c> and nothing else. Scoping
    /// the count to the enrolment row reads as more correct than it is: it would reset the quota on
    /// cancel-then-resubscribe and would not carry across a mid-month plan switch, which the owner ruled
    /// it must.
    /// </summary>
    [Fact]
    public async Task TheCountIsKeyedOnTheUserAndPeriodOnly()
    {
        ArrangeMembership();

        await CreateResolver()
            .ResolveForUserAsync(UserId, ExpressCleaningUtc, NowUtc, CancellationToken.None);

        _usage.Verify(
            u => u.CountLiveInPeriodAsync(
                UserId, MembershipBenefitKind.ExpressUpgrade, PeriodKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NoMembership_WaivesNothing()
    {
        _memberships
            .Setup(r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var waiver = await CreateResolver()
            .ResolveForUserAsync(UserId, ExpressCleaningUtc, NowUtc, CancellationToken.None);

        Assert.False(waiver.Waived);
        Assert.Equal(0, waiver.Quota);
    }

    /// <summary>
    /// The window is <see cref="BookingPolicy"/>'s, evaluated against the ONE threaded clock — the
    /// resolver never re-encodes [2h, 4h) and never reads <c>DateTime.UtcNow</c> itself.
    /// </summary>
    [Theory]
    [InlineData(1.9, false)]
    [InlineData(2.0, true)]
    [InlineData(3.99, true)]
    [InlineData(4.0, false)]
    public async Task TheWindowIsBookingPolicysAndUsesTheThreadedClock(double leadHours, bool expected)
    {
        ArrangeMembership();

        var waiver = await CreateResolver().ResolveForUserAsync(
            UserId, NowUtc.AddHours(leadHours), NowUtc, CancellationToken.None);

        Assert.Equal(expected, waiver.InExpressWindow);
        Assert.Equal(expected, waiver.Waived);
    }
}
