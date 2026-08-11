using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

/// <inheritdoc cref="IExpressWaiverConsumer"/>
public sealed class ExpressWaiverConsumer(
    IExpressWaiverResolver resolver,
    IMembershipBenefitUsageRepository benefitUsageRepository,
    ILogger<ExpressWaiverConsumer> logger) : IExpressWaiverConsumer
{
    public Task<ExpressWaiver> ResolveAsync(
        string? userId,
        DateTime? cleaningUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken)
        => resolver.ResolveForUserAsync(userId, cleaningUtc, nowUtc, cancellationToken);

    public async Task<MembershipBenefitUsage?> TryReserveAsync(
        ExpressWaiver waiver,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (!waiver.Waived
            || string.IsNullOrEmpty(waiver.UserId)
            || string.IsNullOrEmpty(waiver.UserMembershipId))
        {
            return null;
        }

        var reserved = await benefitUsageRepository.TryReserveSlotAsync(
            waiver.UserId,
            MembershipBenefitKind.ExpressUpgrade,
            waiver.PeriodKey,
            waiver.UserMembershipId,
            waiver.Quota,
            nowUtc,
            cancellationToken);

        if (reserved == null)
        {
            logger.LogInformation(
                "Express waiver slot unavailable for user {UserId} in period {PeriodKey} (quota {Quota})",
                waiver.UserId, waiver.PeriodKey, waiver.Quota);
        }

        return reserved;
    }

    public async Task AttachOrderAsync(
        MembershipBenefitUsage reservation,
        string orderId,
        CancellationToken cancellationToken)
    {
        var tracked = await benefitUsageRepository.GetTrackedByIdAsync(reservation.Id, cancellationToken);
        if (tracked == null)
        {
            // The row was inserted by the reservation's own committed statement, so a miss here means the
            // orphan sweep is the only thing that will reclaim it. Loud, because the member silently
            // loses a credit until the cutoff passes.
            logger.LogError(
                "Reserved express waiver {UsageId} not found when attaching order {OrderId}; "
                + "the slot will be reclaimed by ReleaseOrphanedReservationsAsync",
                reservation.Id, orderId);
            return;
        }

        tracked.AttachOrder(orderId);
    }

    public async Task<bool> ReleaseForOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        var usage = await benefitUsageRepository.GetLiveByOrderIdAsync(
            orderId, MembershipBenefitKind.ExpressUpgrade, cancellationToken);
        if (usage == null)
        {
            return false;
        }

        benefitUsageRepository.Deactivate(usage);
        return true;
    }

    public async Task<bool> WouldForfeitOnCustomerCancelAsync(
        string orderId,
        bool hasAssignedEmployee,
        CancellationToken cancellationToken)
    {
        if (!hasAssignedEmployee)
        {
            return false;
        }

        var usage = await benefitUsageRepository.GetLiveByOrderIdAsync(
            orderId, MembershipBenefitKind.ExpressUpgrade, cancellationToken);
        return usage != null;
    }
}
