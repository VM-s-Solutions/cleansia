using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// ADR-0045 D6 — the sweep <b>announces</b> a lapsed preferred-cleaner reservation and releases
/// nothing. The release is, and remains, <c>PreferredHoldUntilUtc &lt;= now</c> inside a WHERE clause
/// with no actor: if this timer never runs, every order still opens to the whole board on time. <b>A
/// dead sweep costs a prompt, never a booking.</b>
///
/// <para><b>Its only write is the receipt.</b> It does not touch <c>PreferredHoldUntilUtc</c>, does not
/// touch <c>PreferredEmployeeId</c>, appends no status track, and assigns nobody. It must in particular
/// never call <c>ClearPreferredHold</c>: <c>NewJobsDigestService.ApplyFreshness</c> reads that pair to
/// decide a lapsed order is NEW AGAIN to every other cleaner, so nulling it here would erase the
/// disjunct before the 30-minute digest ever saw it and the order would fall out of the notification
/// channel permanently — board-only, findable solely by someone who happens to scroll.</para>
///
/// <para><b>Five minutes, not fifteen</b>, because the shortest reservation the policy can produce is
/// 48 minutes (the 8 h floor x 0.10) and a 15-minute sweep would add up to 31% to it before the
/// customer heard anything.</para>
///
/// <para><b>Recurring occurrences are excluded</b> (D6.2). The materializer carries the template's
/// preference into every occurrence 7 days ahead, so un-suppressed a weekly template whose favourite
/// never answers produces one customer push per week, forever — and the customer did not initiate that
/// booking and cannot usefully be asked about it at 03:00. The reservation, the cleaner's push and the
/// customer-visible state all still happen; only the interruption is withheld. Same shape, same reason,
/// as <c>CleanupStalePendingOrders</c>' own <c>RecurringTemplateId == null</c> term.</para>
/// </summary>
public class NotifyLapsedPreferredOffers
{
    public record Command(int BatchSize = 200) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.BatchSize).InclusiveBetween(1, 1000);
        }
    }

    public record Response(int NotifiedCount, int Considered);

    public class Handler(
        IOrderRepository orderRepository,
        INotificationProducer notificationProducer,
        ITenantProvider tenantProvider,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;

            // System job — no JWT context. IgnoreQueryFilters to see rows across all tenants, then group
            // and set the override per tenant so the feed row and the outbox message inherit the right
            // TenantId. A tenant-scoped read here would return nothing.
            var lapsed = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => o.PreferredEmployeeId != null
                    && o.PreferredHoldUntilUtc != null
                    && o.PreferredHoldUntilUtc <= nowUtc
                    && o.PreferredOfferLapseNotifiedAt == null
                    && o.RecurringTemplateId == null
                    && o.CurrentStatus != OrderStatus.Completed
                    && o.CurrentStatus != OrderStatus.Cancelled
                    && !o.AssignedEmployees.Any())
                .OrderBy(o => o.PreferredHoldUntilUtc)
                .Take(command.BatchSize)
                .ToListAsync(cancellationToken);

            var notified = 0;
            foreach (var tenantGroup in lapsed.GroupBy(o => o.TenantId ?? string.Empty))
            {
                tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(tenantGroup.Key))
                {
                    tenantProvider.SetTenantOverride(tenantGroup.Key);
                }

                foreach (var order in tenantGroup)
                {
                    try
                    {
                        await PreferredOfferClosedNotifier.NotifyPreferredOfferClosedAsync(
                            order, notificationProducer, nowUtc, cancellationToken);
                        notified++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "Failed to enqueue preferred-offer closure for order {OrderId}",
                            order.Id);
                    }
                }

                // The receipt and the message commit together, so the prompt is durable iff the stamp
                // is. The commit is also what makes the override above mean anything: rows added by
                // this group are stamped from the ambient tenant AT COMMIT TIME, so deferring to one
                // commit at the end would stamp every group with whichever tenant was processed last.
                await unitOfWork.CommitAsync(cancellationToken);
            }

            if (notified > 0)
            {
                logger.LogInformation(
                    "NotifyLapsedPreferredOffers announced {Notified} of {Considered} lapsed reservations",
                    notified, lapsed.Count);
            }

            return BusinessResult.Success(new Response(notified, lapsed.Count));
        }
    }
}
