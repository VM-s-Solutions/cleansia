using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// <c>TC-BENEFIT-REVERSAL-*</c> (ADR-0035 D4 / AM-11) — the consumer half of the release rule and the
/// cancel-confirmation disclosure.
///
/// <para>The predicate is <c>!order.AssignedEmployees.Any()</c>, NOT an <c>OrderStatus.Confirmed</c>
/// track. <c>Confirmed</c> has four writers and only one involves a cleaner — the Stripe checkout
/// webhook, cash auto-confirm and the admin override all write it with no cleaner in sight — and
/// <c>TakeOrder</c> appends its track only from <c>New</c>/<c>Pending</c>, so a cleaner taking an
/// already-Confirmed order leaves NO trace at all. The status history is simultaneously false-positive
/// and false-negative; the assignment row is the only durable evidence.</para>
/// </summary>
public class ExpressWaiverReleaseRuleTests
{
    private const string OrderId = "order-release-1";
    private const string UserId = "user-release-1";
    private const string PeriodKey = "C:2026-08";

    private readonly Mock<IExpressWaiverResolver> _resolver = new();
    private readonly Mock<IMembershipBenefitUsageRepository> _usage = new();

    private ExpressWaiverConsumer CreateConsumer()
        => new(_resolver.Object, _usage.Object, NullLogger<ExpressWaiverConsumer>.Instance);

    private MembershipBenefitUsage ArrangeLiveSlotOnOrder()
    {
        var usage = MembershipBenefitUsage.Create(
            UserId, MembershipBenefitKind.ExpressUpgrade, PeriodKey, 0, "membership-1", DateTime.UtcNow);
        usage.AttachOrder(OrderId);
        _usage
            .Setup(u => u.GetLiveByOrderIdAsync(
                OrderId, MembershipBenefitKind.ExpressUpgrade, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usage);
        return usage;
    }

    [Fact]
    public async Task ReleasingAnOrderWithALiveSlot_DeactivatesTheRow()
    {
        var usage = ArrangeLiveSlotOnOrder();

        var released = await CreateConsumer().ReleaseForOrderAsync(OrderId, CancellationToken.None);

        Assert.True(released);
        _usage.Verify(u => u.Deactivate(usage), Times.Once);
    }

    [Fact]
    public async Task ReleasingAnOrderWithNoSlot_IsANoOp()
    {
        _usage
            .Setup(u => u.GetLiveByOrderIdAsync(
                It.IsAny<string>(), It.IsAny<MembershipBenefitKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipBenefitUsage?)null);

        var released = await CreateConsumer().ReleaseForOrderAsync(OrderId, CancellationToken.None);

        Assert.False(released);
        _usage.Verify(u => u.Deactivate(It.IsAny<MembershipBenefitUsage>()), Times.Never);
    }

    /// <summary>
    /// The accepted exploit, disclosed: a customer cancelling an ASSIGNED express booking forfeits the
    /// credit — including when the fee is 0 Kč (inside the 15-minute oops window, and on every cash
    /// order), which is exactly when the forfeiture is otherwise invisible.
    /// </summary>
    [Fact]
    public async Task ForfeitureIsDisclosedWhenACleanerIsAssignedAndASlotIsLive()
    {
        ArrangeLiveSlotOnOrder();

        var forfeits = await CreateConsumer().WouldForfeitOnCustomerCancelAsync(
            OrderId, hasAssignedEmployee: true, CancellationToken.None);

        Assert.True(forfeits);
    }

    /// <summary>
    /// No cleaner was pulled onto the short-notice job, so cancelling releases the slot and there is
    /// nothing to warn about. This is the leg that a <c>hasBeenAccepted</c>-shaped predicate gets wrong:
    /// the payment webhook has already written <c>Confirmed</c> for every paid card order.
    /// </summary>
    [Fact]
    public async Task NothingIsForfeitedWhenNoCleanerIsAssigned()
    {
        ArrangeLiveSlotOnOrder();

        var forfeits = await CreateConsumer().WouldForfeitOnCustomerCancelAsync(
            OrderId, hasAssignedEmployee: false, CancellationToken.None);

        Assert.False(forfeits);
    }

    [Fact]
    public async Task NothingIsForfeitedOnAnOrderThatNeverDrewAWaiver()
    {
        _usage
            .Setup(u => u.GetLiveByOrderIdAsync(
                It.IsAny<string>(), It.IsAny<MembershipBenefitKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipBenefitUsage?)null);

        var forfeits = await CreateConsumer().WouldForfeitOnCustomerCancelAsync(
            OrderId, hasAssignedEmployee: true, CancellationToken.None);

        Assert.False(forfeits);
    }

    /// <summary>A resolved-but-unwaived answer never reaches the reservation statement.</summary>
    [Fact]
    public async Task AnUnwaivedAnswerReservesNothing()
    {
        var reserved = await CreateConsumer().TryReserveAsync(
            new ExpressWaiver(true, Waived: false, 2, 0, PeriodKey, UserId, "membership-1"),
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.Null(reserved);
        _usage.Verify(
            u => u.TryReserveSlotAsync(
                It.IsAny<string>(), It.IsAny<MembershipBenefitKind>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The attach is a CHANGE-TRACKED update on the row the raw reservation inserted, so it rides the
    /// caller's unit of work and EF orders the Orders INSERT ahead of it in the same SaveChangesAsync.
    /// The reservation returns a DETACHED object, so the consumer has to load the tracked one.
    /// </summary>
    [Fact]
    public async Task AttachLoadsTheTrackedRowAndStampsTheOrder()
    {
        var detached = MembershipBenefitUsage.Create(
            UserId, MembershipBenefitKind.ExpressUpgrade, PeriodKey, 0, "membership-1", DateTime.UtcNow);
        var tracked = MembershipBenefitUsage.Create(
            UserId, MembershipBenefitKind.ExpressUpgrade, PeriodKey, 0, "membership-1", DateTime.UtcNow);
        tracked.Id = detached.Id;
        _usage
            .Setup(u => u.GetTrackedByIdAsync(detached.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tracked);

        await CreateConsumer().AttachOrderAsync(detached, OrderId, CancellationToken.None);

        Assert.Equal(OrderId, tracked.OrderId);
        Assert.Null(detached.OrderId);
    }
}
