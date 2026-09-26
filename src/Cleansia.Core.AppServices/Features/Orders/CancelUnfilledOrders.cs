using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The booking reached its slot and nobody was on it. Cancel it, give the money back, and add the
/// apology credit.
///
/// <para><b>Nothing in the platform noticed this before.</b> Every sweep that could have seen it
/// requires an assignment — the two reminder sweeps conjoin <c>AssignedEmployees.Any()</c>, and the
/// preferred-hold machinery is about reservations. So a paid order whose seat was never filled sat
/// past its cleaning time forever: the customer had paid, nobody was coming, and no system anywhere
/// said so. The predicate reads the crew, not the status, because the crew is the fact and the status
/// its summary: a release that empties a <c>Confirmed</c> order walks it back to <c>New</c>, but two
/// releases racing can leave <c>Confirmed</c> standing with nobody on it, and an order dropped to
/// <c>New</c> at its slot is exactly a never-taken one — so both statuses are swept on the same
/// term.</para>
///
/// <para><b>This is the one no-show the platform can prove.</b> Every other version of "the cleaner
/// did not arrive" rests on a missing tap, which is indistinguishable from a cleaner who turned up and
/// forgot to slide to start — which is why the owner ruled a lateness detector must never refund on
/// its own. Here there was nobody to tap. That asymmetry is the entire justification for moving money
/// without a human, and it does not transfer to any other case.</para>
///
/// <para><b>All the money for this failure moves HERE, and only here.</b> A drop does not cancel a
/// booking (owner ruling 2026-09-06), so <c>DropOrder</c> refunds nothing — whether the crew left or
/// never arrived, the same empty seat at the same instant is what triggers payment. One path needs no
/// guard against a second one paying the same customer twice.</para>
///
/// <para><b>The apology goes on both failure paths</b> because they are the same path: owner ruling,
/// "the customer must not be paid less when the platform failed harder". Its amount is the order
/// currency's own <c>Currency.NoShowCredit</c>, authored per currency; a currency with none pays none.
/// → /product/business-rules#money-constants</para>
/// </summary>
public class CancelUnfilledOrders
{
    /// <param name="LookbackHours">
    /// How far back to look. THE COLD-START BOUND, and the reason it is a parameter rather than a
    /// literal: every existing row is unswept, so without it the first production tick would select
    /// the entire history of unfilled orders and refund all of them in one pass, unattended. Six hours
    /// means the first tick can only reach today's.
    /// </param>
    /// <param name="GraceMinutes">
    /// How long past the slot to wait before giving up. A cleaner can still take a job after its
    /// start time — <c>TakeOrder</c> has no lead-time gate at all — so a late fill is a real outcome
    /// and cancelling at the stroke of the hour would cut it off.
    /// </param>
    public record Command(int LookbackHours = 6, int GraceMinutes = 30) : ICommand<Response>;

    public record Response(int CancelledCount, int RefundedCount, int CreditedCount);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.LookbackHours).InclusiveBetween(1, 168);
            RuleFor(x => x.GraceMinutes).InclusiveBetween(0, 240);
        }
    }

    /// <summary>No JWT on a sweep. Matches CleanupStalePendingOrders and ExpireStaleCredit.</summary>
    private const string SystemActor = "system";

    /// <summary>
    /// The statuses that mean the work never began. Deliberately NOT
    /// <c>OrderAvailability.OfferableStatuses</c>, which now also admits <c>OnTheWay</c> and
    /// <c>InProgress</c>: an order somebody started is not an order nobody turned up to, and refunding
    /// it in full on a crew count of zero would pay back a clean that was partly done.
    /// </summary>
    private static readonly OrderStatus[] NeverStarted = [OrderStatus.New, OrderStatus.Confirmed];

    public class Handler(
        IOrderRepository orderRepository,
        ICreditAccountRepository creditAccountRepository,
        IRefundService refundService,
        INotificationProducer notificationProducer,
        ITenantProvider tenantProvider,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(
            Command command, CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var deadline = nowUtc.AddMinutes(-command.GraceMinutes);
            var floor = nowUtc.AddHours(-command.LookbackHours);

            // System job, no JWT: read across tenants, then set the override per group so child rows
            // are stamped correctly at the commit INSIDE the loop.
            //
            // The money term is OrderAvailability's, and for the same reason: an unpaid card order is
            // an abandoned checkout that CleanupStalePendingOrders owns, and a cash RECURRING
            // occurrence is structurally unfillable (it can never satisfy the offerability rule), so
            // sweeping it would cancel and credit the same booking every single week.
            //
            // The currency rides along because the apology credit is read off it.
            var unfilled = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => NeverStarted.Contains(o.CurrentStatus)
                    && !o.AssignedEmployees.Any()
                    && o.CleaningDateTime <= deadline
                    && o.CleaningDateTime >= floor
                    && (o.PaymentStatus == PaymentStatus.Paid
                        || (o.PaymentType == PaymentType.Cash && o.RecurringTemplateId == null)))
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees)
                .Include(o => o.Currency)
                .ToListAsync(cancellationToken);

            var cancelled = 0;
            var refunded = 0;
            var credited = 0;

            foreach (var tenantGroup in unfilled.GroupBy(o => o.TenantId ?? string.Empty))
            {
                tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(tenantGroup.Key))
                {
                    tenantProvider.SetTenantOverride(tenantGroup.Key);
                }

                foreach (var order in tenantGroup)
                {
                    // Re-check against the tracked graph. A cleaner can take a job after its start
                    // time, so a seat filled between the read above and this line must not be
                    // cancelled out from under them.
                    if (order.AssignedEmployees.Count > 0
                        || !NeverStarted.Contains(order.CurrentStatus))
                    {
                        continue;
                    }

                    // Platform fault: no fee, full refund. The customer did nothing wrong and the
                    // clean did not happen.
                    order.Cancel(
                        nowUtc,
                        CancelledBy.System,
                        feeRate: 0m,
                        refundAmount: order.TotalPrice,
                        reason: OrderCancellationReasons.NoCleanerAvailable);
                    order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
                    cancelled++;

                    // THE REPEAT SUPPRESSOR IS THE CANCEL ITSELF. Cancelled is outside NeverStarted, so
                    // the next tick's own status filter excludes the row — the CleanupStalePendingOrders
                    // shape, and the reason this sweep needs no stamp column and therefore no schema.
                    // A partial failure re-enters safely: the refund key is deterministic and resolves
                    // to the existing refund, and the credit's idempotency key is unique per order.

                    if (order.PaymentType == PaymentType.Card
                        && order.PaymentStatus == PaymentStatus.Paid
                        && order.TotalPrice > 0m
                        && order.HasRefundableChargeSurface)
                    {
                        var refund = await refundService.IssueRefundAsync(
                            new RefundRequest(
                                order.Id,
                                order.TotalPrice,
                                RefundReason.ServiceNotRendered,
                                SystemActor),
                            cancellationToken);

                        if (refund.IsSuccess)
                        {
                            refunded++;
                        }
                        else
                        {
                            // Say so and carry on. The cancellation is still right, and a refund that
                            // did not go through is a thing a person must see — not a reason to leave
                            // the customer holding a booking nobody is coming to.
                            logger.LogError(
                                "CancelUnfilledOrders could not refund order {OrderId}: {Error}",
                                order.Id, refund.Error?.Message);
                        }
                    }

                    // The credit any card refund already returned goes back on its own leg. Separate
                    // from the apology below: this is the customer's own money coming home, that one
                    // is a gift.
                    await creditAccountRepository.ReturnUnpaidOrderCreditAsync(
                        order, SystemActor, cancellationToken);

                    var apology = await TryIssueApologyCreditAsync(order, cancellationToken);
                    var apologised = apology is not null;
                    if (apologised)
                    {
                        credited++;
                    }

                    // ONE message, keyed on the order. A cancellation happens once per order, so the
                    // bare id is a safe subject here — unlike a refund, which an order can see more
                    // than one of.
                    //
                    // Which message depends on what the customer actually got. Owner ruling 2026-09-06
                    // is that the credit is announced explicitly, and the key that says so is sent
                    // only when the credit was really issued: a guest has no account to hold it and
                    // an order in a currency with no authored credit is refused the grant, so both of
                    // those get the plain cancellation. Promising credit nobody received would be
                    // worse than saying less.
                    //
                    // The credit rides along WITH its currency, formatted here: a money figure, not
                    // PII, so it may sit on the lock screen and in the feed row's args.
                    if (!string.IsNullOrEmpty(order.UserId))
                    {
                        var args = new Dictionary<string, string>
                        {
                            ["orderId"] = order.Id,
                            ["orderNumber"] = order.DisplayOrderNumber,
                        };
                        if (apology is { } creditAmount)
                        {
                            args["amount"] = MoneyText.Format(creditAmount, order.Currency!);
                        }

                        await notificationProducer.NotifyAsync(
                            order.UserId,
                            apologised
                                ? NotificationEventCatalog.OrderNoCleanerRefunded
                                : NotificationEventCatalog.OrderCancelled,
                            args,
                            order.TenantId,
                            order.Id,
                            cancellationToken);
                    }
                }

                // Inside the loop: rows are stamped from the ambient tenant AT COMMIT TIME, so one
                // deferred commit would stamp every group with the last tenant seen.
                await unitOfWork.CommitAsync(cancellationToken);
            }

            if (cancelled > 0)
            {
                // LogError, not Warning: this is the platform failing a paying customer, and the
                // Functions host's Sentry integration drops Warning to a breadcrumb.
                logger.LogError(
                    "CancelUnfilledOrders cancelled {Cancelled} orders that reached their slot with no "
                        + "cleaner ({Refunded} refunded, {Credited} credited)",
                    cancelled, refunded, credited);
            }

            return BusinessResult.Success(new Response(cancelled, refunded, credited));
        }

        /// <summary>
        /// The apology credit: the amount issued, or null — without failing the cancellation —
        /// whenever it cannot honestly be given.
        ///
        /// <para><b>A guest gets the refund and no credit</b>, because there is nowhere to put it:
        /// <c>Order.UserId</c> is nullable and <c>CreditAccount.UserId</c> is not, behind an FK to
        /// Users. That is the rule the home page states in five locales.</para>
        ///
        /// <para><b>Only what the order's own currency authors.</b> A credit account keeps the currency
        /// it was opened in and converts nothing, so the amount is <c>Currency.NoShowCredit</c> on the
        /// order's currency and lands in the customer's account in that same currency. A currency with
        /// no figure pays none: fail closed and log, the refund is unaffected (owner ruling
        /// 2026-09-06). Nothing is ever scaled from another currency's figure.</para>
        /// </summary>
        private async Task<decimal?> TryIssueApologyCreditAsync(Order order, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(order.UserId))
            {
                return null;
            }

            var amount = order.Currency?.NoShowCredit;
            if (amount is null or <= 0m)
            {
                logger.LogWarning(
                    "CancelUnfilledOrders skipped the apology credit on order {OrderId}: no apology credit "
                        + "is authored for {CurrencyCode}. The refund was not affected.",
                    order.Id, order.Currency?.Code ?? order.CurrencyId);
                return null;
            }

            var account = await creditAccountRepository.EnsureForUserAsync(
                order.UserId, order.CurrencyId, cancellationToken);
            if (account is null)
                return null;

            // One key per ORDER, so an order swept twice — a retried tick, a re-entry after a failed
            // commit — pays the apology once. The ledger's IdempotencyKey carries a plain unique index
            // that collapses the second write.
            account.Issue(
                amount: amount.Value,
                reason: CreditTransactionReason.CleanerNoShow,
                idempotencyKey: $"cleaner-noshow:{order.Id}",
                // "system", not the cleaner who walked and not an admin: no person decided this, and
                // writing a partner's id onto a customer's money record would be worse than anonymous.
                issuedBy: SystemActor,
                orderId: order.Id,
                note: "No cleaner was assigned when the booking's time arrived.");

            return amount.Value;
        }
    }
}
