using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Memberships;

/// <summary>
/// Daily sweep dispatching two membership-lifecycle pushes: a renewal reminder ~3 days before the
/// period ends, and a cancellation-effective warning ~1 day before, so the user can still retract.
///
/// <para><b>Idempotency is the two sent-at stamps on the membership row</b>, which the sweep filters
/// on; period rollovers and plan swaps re-arm them.
/// → /flows/loyalty-and-memberships</para>
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
        INotificationProducer notificationProducer,
        IUnitOfWork unitOfWork,
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
                    await notificationProducer.NotifyAsync(
                        membership.UserId,
                        NotificationEventCatalog.MembershipExpiringSoon,
                        new Dictionary<string, string>(),
                        membership.TenantId,
                        membership.Id,
                        cancellationToken);

                    membership.MarkRenewalReminderSent(now);
                    await unitOfWork.CommitAsync(cancellationToken);
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
                    await notificationProducer.NotifyAsync(
                        membership.UserId,
                        NotificationEventCatalog.MembershipCancellationEffective,
                        new Dictionary<string, string>(),
                        membership.TenantId,
                        membership.Id,
                        cancellationToken);

                    membership.MarkCancellationReminderSent(now);
                    await unitOfWork.CommitAsync(cancellationToken);
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
