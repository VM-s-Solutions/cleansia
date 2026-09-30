using Cleansia.Core.AppServices.Abstractions;
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
/// <para><b>All the money for this failure moves through <see cref="CleanerNoShowCancellation"/>.</b> A
/// drop does not cancel a booking (owner ruling 2026-09-06), so <c>DropOrder</c> refunds nothing —
/// whether the crew left or never arrived, the same empty seat at the same instant is what triggers
/// payment. An administrator's no-show confirmation runs the same body; an order is cancelled once, so
/// neither can pay the customer a second time.</para>
///
/// <para><b>The apology goes on both failure paths</b> because they are the same path: owner ruling,
/// "the customer must not be paid less when the platform failed harder". Its amount is the order
/// currency's own <c>Currency.NoShowCredit</c>, authored per currency; a currency with none pays none.
/// → /product/business-rules#money-constants</para>
/// </summary>
public class CancelUnfilledOrders
{
    /// <param name="LookbackHours">
    /// How far back to look. THE COLD-START BOUND: every existing row is unswept, so without it the first
    /// production tick would select the entire history of unfilled orders and refund all of them in one
    /// pass, unattended. A week, so a Functions outage of hours or days still leaves every missed order
    /// to the first tick after it.
    /// </param>
    /// <param name="GraceMinutes">
    /// How long past the slot to wait before giving up. A cleaner can still take a job after its
    /// start time — <c>TakeOrder</c> has no lead-time gate at all — so a late fill is a real outcome
    /// and cancelling at the stroke of the hour would cut it off.
    /// </param>
    public record Command(int LookbackHours = 168, int GraceMinutes = 30) : ICommand<Response>;

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
        CleanerNoShowCancellation noShowCancellation,
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
            // occurrence the customer never confirmed was never offered — AutoCancelStaleRecurringOrders
            // retracts it before its slot.
            //
            // The currency rides along because the apology credit is read off it.
            var unfilled = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => NeverStarted.Contains(o.CurrentStatus)
                    && !o.AssignedEmployees.Any()
                    && o.CleaningDateTime <= deadline
                    && o.CleaningDateTime >= floor
                    && (o.PaymentStatus == PaymentStatus.Paid
                        || (o.PaymentType == PaymentType.Cash
                            && (o.RecurringTemplateId == null || o.CustomerConfirmedAt != null))))
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

                    // THE REPEAT SUPPRESSOR IS THE CANCEL ITSELF: Cancelled is outside NeverStarted, so
                    // the next tick's own status filter excludes the row, and a refund that did not go
                    // through is left to RedrivePendingRefunds.
                    var outcome = await noShowCancellation.ExecuteAsync(
                        order, CancelledBy.System, SystemActor, nowUtc, cancellationToken);
                    cancelled++;
                    if (outcome.RefundedAmount is not null)
                    {
                        refunded++;
                    }
                    if (outcome.ApologyAmount is not null)
                    {
                        credited++;
                    }

                    // Per ORDER, inside the tenant group: rows are stamped from the ambient tenant at
                    // commit time, and the flush means the next order's raw credit return never lands
                    // under a tracked balance that a later commit would write back over it.
                    await unitOfWork.CommitAsync(cancellationToken);
                }
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
    }
}
