using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Specifications;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The two per-job reminders a cleaner gets about work they have already taken: one about two hours
/// out, and a nudge close to the start if they still have not set off.
///
/// <para><b>One sweep, not two.</b> They share a query, a cadence and a tenant loop, and differ only in
/// which window an assignment falls into and which stamp it writes. Two features would be two timers,
/// two cron settings and two near-identical files to keep in step.</para>
///
/// <para><b>Per ASSIGNMENT, not per order.</b> An order's crew is <c>ceil(EstimatedTime / 120)</c> and
/// the catalogue contains single 180- and 240-minute services, so a two-seat job sends two of each.
/// The stamps live on <see cref="Domain.Orders.OrderEmployee"/> for the same reason — a scalar on the
/// order would remind the first cleaner and silently skip the second.</para>
///
/// <para><b>The nudge has a precondition the two-hour notice does not — but it is about the CLEANER,
/// not the order.</b> Both reminders come off one query that selects only orders still
/// <see cref="OrderStatus.Confirmed"/>, so that term distinguishes nothing between them; in practice it
/// only ever silences the nudge, because nobody is on the way two hours early. The nudge's own
/// condition is that the cleaner is not <c>OnTheWay</c> or <c>InProgress</c> on some OTHER assignment:
/// back-to-back jobs put the second job's nudge window inside the first, and asking a cleaner holding a
/// mop in someone else's kitchen whether they have set off is exactly the noise this avoids.</para>
///
/// <para>Both are NON-MUTABLE: a cleaner who can silence a reminder about their own booked work can
/// silence the thing that stops them forgetting it. Same ruling the catalog already makes about a job
/// appearing on their schedule. → /architecture/push-notifications#event-catalogue</para>
/// </summary>
public class SendCleanerJobReminders
{
    /// <param name="SoonLeadMinutesLow">Lower edge of the two-hour notice window.</param>
    /// <param name="SoonLeadMinutesHigh">Upper edge of the two-hour notice window.</param>
    /// <param name="NudgeLeadMinutesLow">Lower edge of the not-set-off nudge window.</param>
    /// <param name="NudgeLeadMinutesHigh">Upper edge of the not-set-off nudge window.</param>
    public record Command(
        int SoonLeadMinutesLow = 110,
        int SoonLeadMinutesHigh = 130,
        int NudgeLeadMinutesLow = 25,
        int NudgeLeadMinutesHigh = 40) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.NudgeLeadMinutesLow).InclusiveBetween(1, 240);
            RuleFor(x => x.NudgeLeadMinutesHigh).GreaterThan(x => x.NudgeLeadMinutesLow);
            RuleFor(x => x.SoonLeadMinutesLow).GreaterThan(x => x.NudgeLeadMinutesHigh);
            RuleFor(x => x.SoonLeadMinutesHigh).GreaterThan(x => x.SoonLeadMinutesLow);
        }
    }

    public record Response(int SoonSent, int NudgesSent, int Considered);

    public class Handler(
        IOrderRepository orderRepository,
        INotificationProducer notificationProducer,
        ITenantProvider tenantProvider,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            // TWO ranges OR-ed, each pruned by ITS OWN stamp. ADR-0054 required change 6.
            //
            // One continuous span from the nudge low to the soon high is 105 minutes wide, and 70 of
            // those minutes are in NEITHER window — an order sat in the scan for 21 ticks and did
            // useful work on at most 2. The stamp terms have to be correlated with their own range
            // rather than OR-ed loosely across both: the nudge stamp is written last, so an
            // uncorrelated "either stamp is null" stays true through the whole soon window and the
            // whole dead gap, which is close to a no-op.
            //
            // Still ONE round trip, so both reminders keep reading off a single snapshot of the
            // order's status — the property the single-span form was defending.
            var soonStart = now.AddMinutes(command.SoonLeadMinutesLow);
            var soonEnd = now.AddMinutes(command.SoonLeadMinutesHigh);
            var nudgeStart = now.AddMinutes(command.NudgeLeadMinutesLow);
            var nudgeEnd = now.AddMinutes(command.NudgeLeadMinutesHigh);

            var due = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => o.CurrentStatus == OrderStatus.Confirmed
                    && o.AssignedEmployees.Any()
                    && ((o.CleaningDateTime >= soonStart
                            && o.CleaningDateTime <= soonEnd
                            && o.AssignedEmployees.Any(ae => ae.ReminderSoonSentAt == null))
                        || (o.CleaningDateTime >= nudgeStart
                            && o.CleaningDateTime <= nudgeEnd
                            && o.AssignedEmployees.Any(ae => ae.ReminderNotStartedSentAt == null))))
                .Include(o => o.AssignedEmployees)
                    .ThenInclude(ae => ae.Employee)
                        .ThenInclude(e => e!.User)
                .ToListAsync(cancellationToken);

            // The nudge asks a cleaner to SET OFF. Anyone already OnTheWay or InProgress on another
            // assignment is out working and cannot act on that — back-to-back jobs put the second job's
            // nudge window squarely inside the first job, so this is not an edge case but the normal
            // shape of a full day. Read once for the whole tick, before the tenant loop, because the
            // question is about the cleaner rather than about any one order.
            var candidateIds = due
                .SelectMany(o => o.AssignedEmployees)
                .Select(a => a.EmployeeId)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var alreadyOnAJob = await orderRepository
                .GetEmployeeIdsCurrentlyOnAJobAsync(candidateIds, cancellationToken);

            var soonSent = 0;
            var nudgesSent = 0;
            var considered = 0;

            foreach (var tenantGroup in due.GroupBy(o => o.TenantId ?? string.Empty))
            {
                tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(tenantGroup.Key))
                {
                    tenantProvider.SetTenantOverride(tenantGroup.Key);
                }

                foreach (var order in tenantGroup)
                {
                    var minutesOut = (order.CleaningDateTime - now).TotalMinutes;
                    var soonDue = minutesOut >= command.SoonLeadMinutesLow
                        && minutesOut <= command.SoonLeadMinutesHigh;
                    var nudgeDue = minutesOut >= command.NudgeLeadMinutesLow
                        && minutesOut <= command.NudgeLeadMinutesHigh;

                    if (!soonDue && !nudgeDue)
                    {
                        continue;
                    }

                    foreach (var assignment in order.AssignedEmployees)
                    {
                        considered++;
                        var userId = assignment.Employee?.UserId;
                        if (string.IsNullOrEmpty(userId))
                        {
                            continue;
                        }

                        // One predicate for all three reminder keys. Rejecting a cleaner does not take
                        // them off their live orders, so without this the platform tells somebody it
                        // has just barred from working that their job starts in two hours.
                        if (!JobReminderRecipient.IsEligible(assignment.Employee))
                        {
                            continue;
                        }

                        try
                        {
                            if (soonDue && assignment.ReminderSoonSentAt == null)
                            {
                                await NotifyAsync(
                                    userId, NotificationEventCatalog.ReminderSoon, order, assignment,
                                    cancellationToken);
                                assignment.MarkReminderSoonSent(now);
                                soonSent++;
                            }
                            else if (nudgeDue
                                && assignment.ReminderNotStartedSentAt == null
                                && !alreadyOnAJob.Contains(assignment.EmployeeId))
                            {
                                await NotifyAsync(
                                    userId, NotificationEventCatalog.ReminderNotStarted, order, assignment,
                                    cancellationToken);
                                assignment.MarkReminderNotStartedSent(now);
                                nudgesSent++;
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex,
                                "Failed to enqueue a job reminder for order {OrderId} employee {EmployeeId}",
                                order.Id, assignment.EmployeeId);
                        }
                    }
                }

                // Same reasoning as SendPreCleaningReminders: the stamps and the outbox rows commit
                // together so each is durable iff the other is, and the commit is what makes the
                // override mean anything — rows are stamped from the ambient tenant AT COMMIT TIME, so
                // one deferred commit would stamp every group with whichever tenant went last.
                await unitOfWork.CommitAsync(cancellationToken);
            }

            if (soonSent > 0 || nudgesSent > 0)
            {
                logger.LogInformation(
                    "SendCleanerJobReminders sent {Soon} two-hour notices and {Nudges} nudges over {Considered} assignments",
                    soonSent, nudgesSent, considered);
            }

            return BusinessResult.Success(new Response(soonSent, nudgesSent, considered));
        }

        private Task NotifyAsync(
            string userId,
            string eventKey,
            Domain.Orders.Order order,
            Domain.Orders.OrderEmployee assignment,
            CancellationToken ct) =>
            notificationProducer.NotifyAsync(
                userId,
                eventKey,
                new Dictionary<string, string>
                {
                    ["orderId"] = order.Id,
                    ["orderNumber"] = order.DisplayOrderNumber,
                },
                order.TenantId,
                // The ASSIGNMENT, so a re-tick dedups onto one key per (cleaner, event, assignment)
                // rather than minting a fresh message every sweep — while a genuinely NEW assignment on
                // the same order still gets its own reminder. The order id alone would be the same key
                // for every assignment this order ever had, and the stamps that suppress a re-send live
                // on the assignment row, which an admin reassign hard-deletes and recreates: the stamp
                // resets, the sweep decides to send, and the key it mints is still taken.
                AssignmentNotificationSubject.For(order.Id, assignment.Id),
                ct);
    }
}
