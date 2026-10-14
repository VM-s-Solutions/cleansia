using System.Runtime.ExceptionServices;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Refunds;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Hourly sweep that cancels Pending recurring-template Orders whose
/// confirmation window has lapsed. Frees the cleaner's slot when the
/// customer ignores the 24h-ahead reminder (sent by
/// <c>SendRecurringOrderRemindersHandler</c>) and never confirms.
///
/// Hourly cadence keeps the gap between "should have confirmed" and "slot
/// freed for someone else" short enough that it doesn't affect cleaner
/// scheduling expectations. Idempotent: a cancelled occurrence leaves the
/// sweep's selection by its status, so re-running mid-hour is a no-op.
///
/// <para>The same tick re-drives pending card refunds (<c>RedrivePendingRefunds</c>). Each command is
/// sent from a DI scope of its own, which is what keeps one from hiding the other: a command that throws
/// is logged at Warning, and the invocation fails only after both have run. → /decisions/adr-0072</para>
/// </summary>
public class AutoCancelStaleRecurringOrdersHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<AutoCancelStaleRecurringOrdersHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("AutoCancelStaleRecurringOrders timer triggered at {Time}", DateTime.UtcNow);
        var thrown = new List<Exception>();

        await RunInOwnScopeAsync(nameof(AutoCancelStaleRecurringOrders), thrown, async mediator =>
        {
            var result = await mediator.Send(new AutoCancelStaleRecurringOrders.Command(), ct);
            if (result.IsSuccess && result.Value != null)
            {
                logger.LogInformation(
                    "AutoCancelStaleRecurringOrders completed; cancelled {Cancelled} of {Considered}, {Failed} failed",
                    result.Value.Cancelled,
                    result.Value.Considered,
                    result.Value.Failed);
            }
            else
            {
                logger.LogError(
                    "AutoCancelStaleRecurringOrders failed: {Error}",
                    result.Error?.Message ?? "unknown");
            }
        }, ct);

        // A SECOND command on this hourly schedule rather than a timer of its own (the argument
        // CleanupStalePendingOrdersHandler records), sent separately so neither failure hides the other.
        await RunInOwnScopeAsync(nameof(RedrivePendingRefunds), thrown, async mediator =>
        {
            var refunds = await mediator.Send(new RedrivePendingRefunds.Command(), ct);
            if (refunds.IsSuccess && refunds.Value != null)
            {
                logger.LogInformation(
                    "RedrivePendingRefunds completed; re-drove {Redriven} of {Considered}, alerted {Alerted}",
                    refunds.Value.Redriven,
                    refunds.Value.Considered,
                    refunds.Value.Alerted);
            }
            else
            {
                logger.LogError(
                    "RedrivePendingRefunds failed: {Error}",
                    refunds.Error?.Message ?? "unknown");
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
                "{Command} threw; the other command on this tick still runs, and the invocation fails after the last",
                command);
            thrown.Add(ex);
        }
    }
}
