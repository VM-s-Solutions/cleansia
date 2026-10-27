using System.Runtime.ExceptionServices;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Features.Orders;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Every 15 minutes, sweep card orders that have been sitting in Pending for
/// more than an hour and mark them Cancelled. Picks up users who opened
/// PaymentSheet on mobile and closed it without paying — without this they'd
/// stay visible to cleaners (matching pool pollution) until Stripe eventually
/// expires the underlying PaymentIntent ~24h later.
///
/// <para>FOUR commands ride this one schedule. Each is sent separately so a failure in one does not
/// suppress the others, and each logs its own outcome. A new timer costs a trigger shell, a handler, a
/// DI registration, a cron token in two settings files, a bicep entry and two test rows — and the
/// eight tokenised timers silently never fired in Azure for months, which is the argument against
/// adding a ninth for work an existing tick can carry.</para>
///
/// <para>What makes that true is the DI scope each command is sent from: one that throws takes its
/// change tracker and tenant override away with its scope, is logged at Warning, and fails the
/// invocation only after the last command has run. → /decisions/adr-0072</para>
/// </summary>
public class CleanupStalePendingOrdersHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<CleanupStalePendingOrdersHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("CleanupStalePendingOrders timer triggered at {Time}", DateTime.UtcNow);
        var thrown = new List<Exception>();

        await RunInOwnScopeAsync(nameof(CleanupStalePendingOrders), thrown, async mediator =>
        {
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
        }, ct);

        // ADR-0035 AM-7 — a SECOND command on this existing schedule, not a second schedule. Membership
        // benefit orphans live in MembershipBenefitUsages and are invisible to the sweep above, which
        // reads Orders; an orphan is precisely a reservation whose order never committed. Sent
        // separately so a failure in one sweep does not suppress the other.
        await RunInOwnScopeAsync(nameof(ReleaseOrphanedBenefitReservations), thrown, async mediator =>
        {
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
        }, ct);

        // A THIRD command on the same schedule, and the only one of the three that moves money.
        // Orders that reached their cleaning time with nobody assigned: nothing in the platform saw
        // these before, because every sweep that could have requires an assignment. Fifteen minutes
        // plus the command's own 30-minute grace means a customer hears within about three quarters of
        // an hour of the slot they were waiting through.
        await RunInOwnScopeAsync(nameof(CancelUnfilledOrders), thrown, async mediator =>
        {
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
        }, ct);

        // A FOURTH: a crew job everyone on it marked done two hours ago and nobody completed closes itself
        // through the completion a cleaner's Complete runs (owner ruling 2026-10-26), so it closes 120 to 135
        // minutes after the last mark.
        await RunInOwnScopeAsync(nameof(AutoCompleteMarkedDoneOrders), thrown, async mediator =>
        {
            var closed = await mediator.Send(new AutoCompleteMarkedDoneOrders.Command(), ct);
            if (closed.IsSuccess && closed.Value != null)
            {
                logger.LogInformation(
                    "AutoCompleteMarkedDoneOrders completed; completed {Completed} of {Considered}, failed {Failed}",
                    closed.Value.Completed,
                    closed.Value.Considered,
                    closed.Value.Failed);
            }
            else
            {
                logger.LogError(
                    "AutoCompleteMarkedDoneOrders failed: {Error}",
                    closed.Error?.Message ?? "unknown");
            }
        }, ct);

        if (thrown.Count == 1)
        {
            ExceptionDispatchInfo.Capture(thrown[0]).Throw();
        }

        if (thrown.Count > 1)
        {
            throw new AggregateException(thrown);
        }
    }

    private async Task RunInOwnScopeAsync(
        string command, List<Exception> thrown, Func<IMediator, Task> send, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await send(scope.ServiceProvider.GetRequiredService<IMediator>());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "{Command} threw; the other commands on this tick still run, and the invocation fails after the last",
                command);
            thrown.Add(ex);
        }
    }
}
