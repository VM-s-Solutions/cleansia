using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The cleaner comes off the job outright. The seat goes back on the board and the booking is NOT
/// cancelled — owner ruling 2026-09-06: "put the seat back on the board … if there is at least 1
/// cleaner then it's possible to complete the cleaning".
///
/// <para><b>This is the first thing in the platform that has ever released a seat.</b> Nothing else
/// does: <c>AdminReassignOrder</c> and <c>RejectEmployee</c> are admin acts, and a cleaner previously
/// had no way out at all. That is why the two discovery mechanisms it depends on — the same-value
/// status track and <c>Order.HasTakeableSeat</c> — had never been exercised against an empty seat.</para>
///
/// <para><b>It moves no money.</b> Not the refund, not the 250. Under the same ruling a drop cannot
/// cancel a booking, so there is nothing to refund at this moment — the customer has lost a cleaner,
/// not their clean. All of that lives in one place instead: the sweep that finds an order arriving at
/// its slot with nobody on it. Two money paths for one failure would have needed a guard between them
/// against refunding twice; one path needs none.</para>
///
/// <para><b>Prefer <see cref="RequestCover"/>.</b> A drop leaves the seat empty from this instant; a
/// cover request keeps the cleaner on the hook until somebody actually takes it. The partner app
/// should offer cover first and this as the way out when nobody answers.</para>
/// </summary>
public class DropOrder
{
    public record Command(string OrderId) : ICommand<Response>;

    /// <param name="CrewRemaining">
    /// How many cleaners are still on the job. Zero means the order is now heading for the sweep
    /// unless somebody takes it — the partner app uses this to say so plainly rather than letting the
    /// cleaner walk away believing the booking is covered.
    /// </param>
    public record Response(string OrderId, int CrewRemaining);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IOrderAccessService orderAccessService,
        IEmployeeActionAuditRepository employeeActionAuditRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(
            Command command, CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);

            if (string.IsNullOrEmpty(employeeId))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            // OrderStatusHistory is not optional — AddOrderStatus derives Sequence from it and
            // recomputes CurrentStatus, so a partial load restates the order's status.
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order is null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            // Already off it. A no-op success, not an error: the caller's desired state holds, and the
            // same idempotent silence DeclinePreferredOffer gives a second decline.
            if (order.AssignedEmployees.All(oe => oe.EmployeeId != employeeId))
            {
                return BusinessResult.Success(
                    new Response(order.Id, order.AssignedEmployees.Count));
            }

            if (!OrderAvailability.OfferableStatuses.Contains(order.CurrentStatus))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotConfirmed));
            }

            // Was the hold this cleaner's own? Read BEFORE the unassign, because the answer stops
            // being recoverable once the row is gone.
            var heldByCaller = PreferredOffer.HasLiveReservation(
                    order.PreferredEmployeeId, order.PreferredHoldUntilUtc, nowUtc)
                && order.PreferredEmployeeId == employeeId;

            order.UnassignEmployee(employeeId);

            // A live hold makes the order invisible to EVERYONE but its beneficiary, for up to 12
            // hours. Leaving one standing after its own beneficiary walks away would take the seat
            // this command just freed and hide it from the entire board — the exact opposite of
            // putting it back. Only the caller's own hold is ended: another cleaner's reservation is
            // not this cleaner's to cancel.
            if (heldByCaller)
            {
                order.EndPreferredHold(nowUtc);
            }

            // Re-advertise. The digest's freshness test is
            // OrderStatusHistory.Any(s => s.CreatedOn > since), so without a new row this seat is
            // invisible to every cleaner forever. Same value, so nothing else in the platform reads a
            // change. → NewJobsDigestService
            order.AddOrderStatus(OrderStatusTrack.Create(order.CurrentStatus, order));

            employeeActionAuditRepository.Add(EmployeeActionAudit.Create(
                employeeId, order.Id, EmployeeAuditAction.OrderDropped));

            return BusinessResult.Success(
                new Response(order.Id, order.AssignedEmployees.Count));
        }
    }
}
