using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Features.Orders;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Every 15 minutes, sweep card orders that have been sitting in Pending for
/// more than an hour and mark them Cancelled. Picks up users who opened
/// PaymentSheet on mobile and closed it without paying — without this they'd
/// stay visible to cleaners (matching pool pollution) until Stripe eventually
/// expires the underlying PaymentIntent ~24h later.
///
/// <para>THREE commands ride this one schedule. Each is sent separately so a failure in one does not
/// suppress the others, and each logs its own outcome. A new timer costs a trigger shell, a handler, a
/// DI registration, a cron token in two settings files, a bicep entry and two test rows — and the
/// eight tokenised timers silently never fired in Azure for months, which is the argument against
/// adding a ninth for work an existing tick can carry.</para>
/// </summary>
public class CleanupStalePendingOrdersHandler(
    IMediator mediator,
    ILogger<CleanupStalePendingOrdersHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("CleanupStalePendingOrders timer triggered at {Time}", DateTime.UtcNow);
        var result = await mediator.Send(new CleanupStalePendingOrders.Command(OlderThanHours: 1), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "CleanupStalePendingOrders completed; cancelled {Count} orders",
                result.Value.CancelledCount);
        }
        else
        {
            logger.LogError(
                "CleanupStalePendingOrders failed: {Error}",
                result.Error?.Message ?? "unknown");
        }

        // ADR-0035 AM-7 — a SECOND command on this existing schedule, not a second schedule. Membership
        // benefit orphans live in MembershipBenefitUsages and are invisible to the sweep above, which
        // reads Orders; an orphan is precisely a reservation whose order never committed. Sent
        // separately so a failure in one sweep does not suppress the other.
        var orphans = await mediator.Send(
            new ReleaseOrphanedBenefitReservations.Command(OlderThanHours: 1), ct);
        if (orphans.IsSuccess && orphans.Value != null)
        {
            logger.LogInformation(
                "ReleaseOrphanedBenefitReservations completed; released {Count} reservations",
                orphans.Value.ReleasedCount);
        }
        else
        {
            logger.LogError(
                "ReleaseOrphanedBenefitReservations failed: {Error}",
                orphans.Error?.Message ?? "unknown");
        }

        // A THIRD command on the same schedule, and the only one of the three that moves money.
        // Orders that reached their cleaning time with nobody assigned: nothing in the platform saw
        // these before, because every sweep that could have requires an assignment. Fifteen minutes
        // plus the command's own 30-minute grace means a customer hears within about three quarters of
        // an hour of the slot they were waiting through.
        var unfilled = await mediator.Send(new CancelUnfilledOrders.Command(), ct);
        if (unfilled.IsSuccess && unfilled.Value != null)
        {
            logger.LogInformation(
                "CancelUnfilledOrders completed; cancelled {Cancelled}, refunded {Refunded}, credited {Credited}",
                unfilled.Value.CancelledCount,
                unfilled.Value.RefundedCount,
                unfilled.Value.CreditedCount);
        }
        else
        {
            logger.LogError(
                "CancelUnfilledOrders failed: {Error}",
                unfilled.Error?.Message ?? "unknown");
        }
    }
}
