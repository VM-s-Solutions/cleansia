using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Memberships;

/// <summary>
/// Daily sweep that dispatches two membership-lifecycle pushes:
/// <list type="bullet">
///   <item><c>membership.expiring_soon</c> — fired ~3 days before
///   <see cref="UserMembership.CurrentPeriodEnd"/> for any
///   <see cref="MembershipStatus.Active"/> subscription. Acts as a
///   "your plan renews soon" billing reminder.</item>
///   <item><c>membership.cancellation_effective</c> — fired ~1 day before
///   <see cref="UserMembership.CurrentPeriodEnd"/> when
///   <see cref="UserMembership.CancelledAt"/> is set. Acts as a "your
///   cancellation takes effect tomorrow — benefits end" warning so the
///   user can still retract via a plan swap.</item>
/// </list>
///
/// Idempotency: each membership row carries
/// <see cref="UserMembership.RenewalReminderSentAt"/> and
/// <see cref="UserMembership.CancellationReminderSentAt"/> stamps which the
/// sweep filters on. Period rollovers (in <see cref="UserMembership.UpdateFromStripeWebhook"/>)
/// and plan swaps re-arm the stamps so future periods/cancellations fire
/// fresh reminders.
///
/// Both queries run cross-tenant — the sweep is invoked system-level (no
/// JWT). The queue dispatch carries each membership's TenantId so the
/// downstream dispatcher resolves to the correct partition.
/// </summary>
public class SendMembershipLifecycleNotifications
{
    public record Command(
        int RenewalLeadDaysLow = 2,
        int RenewalLeadDaysHigh = 4,
        int CancellationLeadDaysHigh = 2) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.RenewalLeadDaysLow).InclusiveBetween(1, 30);
            RuleFor(x => x.RenewalLeadDaysHigh).GreaterThan(x => x.RenewalLeadDaysLow);
            RuleFor(x => x.CancellationLeadDaysHigh).InclusiveBetween(1, 30);
        }
    }

    public record Response(int RenewalRemindersSent, int CancellationRemindersSent);

    public class Handler(
        IUserMembershipRepository membershipRepository,
        IQueueClient queueClient,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var renewalWindowStart = now.AddDays(command.RenewalLeadDaysLow);
            var renewalWindowEnd = now.AddDays(command.RenewalLeadDaysHigh);
            var cancellationWindowEnd = now.AddDays(command.CancellationLeadDaysHigh);

            // Renewal reminders: Active subs whose period ends in [now+2d, now+4d]
            // and haven't been reminded for THIS period yet. The stamp is cleared
            // by the period-rollover branch in UpdateFromStripeWebhook so the
            // next period gets its own reminder.
            var renewalDue = await membershipRepository.GetQueryableIgnoringTenant()
                .Where(m => m.Status == MembershipStatus.Active
                    && m.RenewalReminderSentAt == null
                    && m.CurrentPeriodEnd >= renewalWindowStart
                    && m.CurrentPeriodEnd <= renewalWindowEnd)
                .ToListAsync(cancellationToken);

            var renewalSent = 0;
            foreach (var membership in renewalDue)
            {
                try
                {
                    await queueClient.SendAsync(
                        QueueNames.NotificationsDispatch,
                        new SendPushNotificationMessage(
                            UserId: membership.UserId,
                            EventKey: NotificationEventCatalog.MembershipExpiringSoon,
                            Args: new Dictionary<string, string>(),
                            TenantId: membership.TenantId),
                        cancellationToken);

                    membership.MarkRenewalReminderSent(now);
                    renewalSent++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to enqueue renewal reminder for membership {MembershipId}",
                        membership.Id);
                }
            }

            // Cancellation-effective reminders: subs that the user requested to
            // cancel (CancelledAt set), benefits still applying (Status Active),
            // and ending within the next CancellationLeadDaysHigh. We don't gate
            // by a low bound — a same-day-ending membership should still get a
            // last-chance push.
            var cancellationDue = await membershipRepository.GetQueryableIgnoringTenant()
                .Where(m => m.CancelledAt != null
                    && m.CancellationReminderSentAt == null
                    && m.Status == MembershipStatus.Active
                    && m.CurrentPeriodEnd >= now
                    && m.CurrentPeriodEnd <= cancellationWindowEnd)
                .ToListAsync(cancellationToken);

            var cancellationSent = 0;
            foreach (var membership in cancellationDue)
            {
                try
                {
                    await queueClient.SendAsync(
                        QueueNames.NotificationsDispatch,
                        new SendPushNotificationMessage(
                            UserId: membership.UserId,
                            EventKey: NotificationEventCatalog.MembershipCancellationEffective,
                            Args: new Dictionary<string, string>(),
                            TenantId: membership.TenantId),
                        cancellationToken);

                    membership.MarkCancellationReminderSent(now);
                    cancellationSent++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to enqueue cancellation reminder for membership {MembershipId}",
                        membership.Id);
                }
            }

            return BusinessResult.Success(new Response(renewalSent, cancellationSent));
        }
    }
}
