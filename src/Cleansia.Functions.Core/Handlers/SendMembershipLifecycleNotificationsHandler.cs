using Cleansia.Core.AppServices.Features.Memberships;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Daily sweep that dispatches two membership-lifecycle pushes:
/// <c>membership.expiring_soon</c> (renewal reminder, ~3 days before period
/// end) and <c>membership.cancellation_effective</c> (~1 day before benefits
/// stop for users with a pending cancellation).
///
/// Same dev cron cadence as the other recurring sweeps — the handler is
/// idempotent via per-membership stamps so over-firing is harmless. In
/// production this can be tightened to a daily slot (e.g. 03:00 UTC) once
/// telemetry confirms behaviour.
/// </summary>
public class SendMembershipLifecycleNotificationsHandler(
    IMediator mediator,
    ILogger<SendMembershipLifecycleNotificationsHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation(
            "SendMembershipLifecycleNotifications timer triggered at {Time}",
            DateTime.UtcNow);
        var result = await mediator.Send(new SendMembershipLifecycleNotifications.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "SendMembershipLifecycleNotifications completed; sent {RenewalSent} renewal + {CancellationSent} cancellation reminders",
                result.Value.RenewalRemindersSent,
                result.Value.CancellationRemindersSent);
        }
        else
        {
            logger.LogError(
                "SendMembershipLifecycleNotifications failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
