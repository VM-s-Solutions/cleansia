using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Sweep one-off card orders stuck in <see cref="PaymentStatus.Pending"/> past
/// the 1-hour mark. These are typically users who opened PaymentSheet but
/// closed it without confirming. Leaving them in Pending pollutes the matching
/// pool (cleaners can't pick them up), confuses admin dashboards, and races
/// with Stripe's own ~24h PaymentIntent expiry.
///
/// Marks them Cancelled and tells the customer. Webhook handlers for any
/// eventually-canceled PaymentIntent will then no-op via the existing
/// idempotency check.
///
/// Credit a checkout spent comes back in the commit that cancels it. A company
/// whose commit is refused keeps its orders and their credit as they were, and
/// the next run takes them again.
///
/// Recurring occurrences are deliberately out of scope — see the query below;
/// <see cref="Cleansia.Core.AppServices.Features.Bookings.AutoCancelStaleRecurringOrders"/>
/// owns their retraction.
/// </summary>
public class CleanupStalePendingOrders
{
    /// <param name="BatchSize">
    /// How many checkouts one run may cancel, oldest first. A larger backlog spreads over the following
    /// runs with no cursor to carry, because a cancelled order has left the query.
    /// → /flows/cross-cutting#tenancy
    /// </param>
    public record Command(int OlderThanHours = 1, int BatchSize = 500) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OlderThanHours).InclusiveBetween(1, 168);
            RuleFor(x => x.BatchSize).InclusiveBetween(1, 5000);
        }
    }

    public record Response(int CancelledCount);

    /// <summary>No JWT on a sweep. Matches ExpireStaleReferrals and LoyaltyService.</summary>
    private const string SystemActor = "system";

    public class Handler(
        IOrderRepository orderRepository,
        ICreditAccountRepository creditAccountRepository,
        INotificationProducer notificationProducer,
        GuestOrderAccessTokenIssuer guestAccessTokenIssuer,
        IPendingDispatch pending,
        ITenantProvider tenantProvider,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-command.OlderThanHours);

            // System job — no JWT context. Read across all tenants, then group and set the override per
            // tenant: it is what stamps the OrderStatusTrack each group adds. It does not decide the
            // notices. The customer's feed row and push carry the customer's own account company and the
            // guest e-mail the order's, each passed explicitly. → /flows/cross-cutting#tenancy
            //
            // RecurringTemplateId == null is load-bearing. A recurring occurrence is materialized up
            // to 7 days before its slot and stays Pending until the customer confirms it via
            // ConfirmRecurringOrder, so "Pending for over an hour" is its NORMAL state, not an
            // abandoned checkout. Retracting unconfirmed occurrences belongs to
            // AutoCancelStaleRecurringOrders, which fires at T-1h once the reminder has gone
            // unanswered; this term is the exact complement of that sweep's RecurringTemplateId !=
            // null, so every Pending order has exactly one retractor.
            //
            // An order already cancelled is not an abandoned checkout, however its money axis reads — a
            // cancel before paying leaves it Pending, and anonymisation clears RecurringTemplateId on old
            // cancelled occurrences. Re-cancelling one would overwrite who cancelled it and why, and stage
            // the guest e-mail again under a key its first cancellation already wrote: a commit that fails
            // on every tick and takes the rest of the sweep with it.
            var stale = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => o.PaymentStatus == PaymentStatus.Pending
                    && o.PaymentType == PaymentType.Card
                    && o.RecurringTemplateId == null
                    && o.CurrentStatus != OrderStatus.Cancelled
                    && o.CreatedOn < cutoff)
                .Include(o => o.OrderStatusHistory)
                .OrderBy(o => o.CreatedOn)
                .ThenBy(o => o.Id)
                .Take(command.BatchSize)
                .ToListAsync(cancellationToken);

            int cancelledCount = 0;
            foreach (var tenantGroup in stale.GroupBy(o => o.TenantId ?? string.Empty))
            {
                // Reset before each iteration so the previous group's override
                // never outlives its group: what the next commit stamps is
                // decided here, not by whatever ran last.
                tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(tenantGroup.Key))
                {
                    tenantProvider.SetTenantOverride(tenantGroup.Key);
                }

                // Before the loop, so one read of the recipients' companies serves the whole company.
                await notificationProducer.NotifyEachAsync(
                    [
                        .. tenantGroup
                            .Where(order => order.PaymentStatus == PaymentStatus.Pending && !string.IsNullOrEmpty(order.UserId))
                            .Select(order => (order.UserId!, NotificationEventCatalog.OrderCancelled,
                                new Dictionary<string, string>
                                {
                                    ["orderId"] = order.Id,
                                    ["orderNumber"] = order.DisplayOrderNumber,
                                },
                                order.Id))
                    ],
                    cancellationToken);

                // Each credit return holds its customer's lock until the commit below, so the locks are taken
                // in one fixed order, ordinal user id, as the referral grant takes them. → /decisions/adr-0073
                foreach (var order in tenantGroup
                             .OrderBy(o => o.UserId, StringComparer.Ordinal)
                             .ThenBy(o => o.Id, StringComparer.Ordinal))
                {
                    if (order.PaymentStatus != PaymentStatus.Pending)
                    {
                        continue;
                    }

                    order.UpdatePaymentStatus(PaymentStatus.Failed);
                    order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

                    // Record WHY, not just that. The push tells the customer to tap for the reason and
                    // the order detail had none to show, so a cancelled booking read as arbitrary —
                    // the customer had to infer a failed payment from the absence of a charge.
                    // Fee-free and refund-free by construction: nothing was ever captured.
                    order.Cancel(
                        DateTime.UtcNow,
                        CancelledBy.System,
                        feeRate: 0m,
                        refundAmount: 0m,
                        reason: OrderCancellationReasons.PaymentNotCompleted);

                    // Nothing was captured, but credit may have been SPENT - CreateOrder debits it
                    // before the customer ever reaches Stripe. The return joins this company's commit,
                    // onto the customer's credit account, whose company comes from the account and not
                    // from the override; its key stops a rerun returning it twice. → /flows/cross-cutting#tenancy
                    await creditAccountRepository.ReturnUnpaidOrderCreditAsync(
                        order, cardRefunded: 0m, SystemActor, cancellationToken);

                    await GuestCancellationEmail.EnqueueAsync(order, EmailLocale.Resolve(order.LanguageCode),
                        successfulRefundAmount: null, guestAccessTokenIssuer, pending, cancellationToken);

                    cancelledCount++;
                }

                // The commit is what makes the override above mean anything: rows added by this group
                // are stamped from the ambient tenant AT COMMIT TIME, so deferring to the pipeline's
                // single commit would stamp every group with whichever tenant was processed last. It
                // commits the group's credit returns too, or rolls them back with the group.
                //
                // No catch: a refused group ends the run and its scope. Catching to continue would let
                // this group's leftovers ride the next group's commit, without the credit the failed
                // commit rolled back (→ ADR-0072 D2).
                await unitOfWork.CommitAsync(cancellationToken);
            }

            if (cancelledCount > 0)
            {
                logger.LogInformation(
                    "CleanupStalePendingOrders cancelled {Count} stale card orders older than {Hours}h",
                    cancelledCount, command.OlderThanHours);
            }

            return BusinessResult.Success(new Response(cancelledCount));
        }
    }
}
