using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The cleaner cannot make a job and asks for somebody to take it off them.
///
/// <para><b>They stay assigned.</b> Owner ruling 2026-09-06, and it is the whole difference between
/// this and <see cref="DropOrder"/>: the seat is still filled, the job is still deliverable if nobody
/// answers, and the customer's T-1h reminder still fires. What changes is that the seat becomes
/// TAKEABLE — a taker DISPLACES the holder rather than sitting beside them, which is why
/// <c>Order.HasTakeableSeat</c> had to become a separate question from
/// <c>Order.HasAvailableSpots</c>.</para>
///
/// <para><b>Why it appends a same-value status track.</b> The digest decides an order is new to a
/// cleaner with <c>OrderStatusHistory.Any(s =&gt; s.CreatedOn &gt; since)</c>. An order whose status does
/// not change is therefore never re-advertised, and a seat released into silence reaches nobody at all
/// — which is the state the platform was in before this command existed, because nothing had ever
/// released a seat. <c>AddOrderStatus</c> has no same-value guard and recomputes <c>CurrentStatus</c>
/// to the value it already held, so the row is inert everywhere except that freshness test.</para>
///
/// <para><b>No reason field, deliberately.</b> Nobody has ruled what the reasons are or what is done
/// with them, and ADR-0045 D13 refuses free text collected just in case — it would also put a
/// cleaner's prose on the logged wire surface. The row this writes says WHO and WHEN, which is what
/// the owner asked to be able to see.</para>
/// </summary>
public class RequestCover
{
    public record Command(string OrderId) : ICommand<Response>;

    /// <param name="CoverRequestedAt">
    /// The stamp that was kept. First tap wins, so a second one returns the first one's time rather
    /// than moving the deadline — which is why this command needs no "already requested" error key.
    /// </param>
    public record Response(string OrderId, DateTime CoverRequestedAt);

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
        IEmployeeRepository employeeRepository,
        INotificationProducer notificationProducer,
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

            // OrderStatusHistory is not optional: AddOrderStatus derives the new row's Sequence from
            // the collection and recomputes CurrentStatus from it, so a partial load would write a
            // duplicate sequence and silently restate the order's status.
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

            var assignment = order.AssignedEmployees
                .FirstOrDefault(oe => oe.EmployeeId == employeeId);

            if (assignment is null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.EmployeeNotAssignedToOrder));
            }

            // "Not over" rather than "not started": a cleaner who is already travelling, or who has
            // begun and been taken ill, is exactly who needs this. The coarse offerability floor IS
            // that set now, so the gate cannot drift from what a replacement is allowed to take.
            if (!OrderAvailability.OfferableStatuses.Contains(order.CurrentStatus))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotConfirmed));
            }

            // Idempotent by construction — MarkCoverRequested is ??=, so a second tap keeps the first
            // stamp and returns success. A repeated request is not an error to a cleaner tapping twice
            // on a bad connection.
            var alreadyRequested = assignment.CoverRequestedAt is not null;
            assignment.MarkCoverRequested(nowUtc);

            if (!alreadyRequested)
            {
                // Re-advertise. See the class comment: without a new status row the digest can never
                // see this seat again.
                order.AddOrderStatus(OrderStatusTrack.Create(order.CurrentStatus, order));

                employeeActionAuditRepository.Add(EmployeeActionAudit.Create(
                    employeeId, order.Id, EmployeeAuditAction.CoverRequested));

                // Wake the cleaners who could take it. The seat is already released and re-advertised
                // above, so this is an accelerant rather than the mechanism: the hourly digest stays
                // the floor if this finds nobody.
                await SeatOpenedNotifier.NotifySeatOpenAsync(
                    order,
                    employeeId,
                    assignment.Id,
                    employeeRepository,
                    notificationProducer,
                    cancellationToken);
            }

            return BusinessResult.Success(
                new Response(order.Id, assignment.CoverRequestedAt!.Value));
        }
    }
}
