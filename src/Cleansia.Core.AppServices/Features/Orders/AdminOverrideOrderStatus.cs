using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

[AuditAction("order.status.override", Sensitive = true, ResourceType = "Order")]
public class AdminOverrideOrderStatus
{
    public record Command(
        string OrderId,
        OrderStatus TargetStatus
    ) : ICommand<Response>;

    public record Response(
        string OrderId,
        OrderStatus Status);

    public record StatusSnapshot(string OrderId, OrderStatus? Status);

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

            RuleFor(x => x.TargetStatus)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider,
        IAuditContext auditContext,
        ILiveActivityProducer liveActivityProducer
    ) : ICommandHandler<Command, Response>
    {
        // The RANK array — not the set of legal targets. It must stay total over every status a row
        // can currently HOLD, Pending included: drop a member and Array.IndexOf returns -1 for a row
        // in that state, which satisfies `targetRank <= currentRank` for every target and inverts
        // the forward-only guard into a licence to walk backwards. Cancelled is absent because no
        // row reaches this code holding it (the terminal check above refuses first) and because
        // cancellation is AdminCancelOrder's, which carries the refund seam.
        private static readonly OrderStatus[] Lifecycle =
        [
            OrderStatus.New,
            OrderStatus.Pending,
            OrderStatus.Confirmed,
            OrderStatus.OnTheWay,
            OrderStatus.InProgress,
            OrderStatus.Completed,
        ];

        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            _ = userSessionProvider.GetUserId()!;
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            var currentStatus = order.CurrentStatus;

            if (currentStatus == OrderStatus.Completed)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderAlreadyCompleted));
            }
            if (currentStatus == OrderStatus.Cancelled)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderAlreadyCancelled));
            }

            // OrderStatus.Pending is dead (ADR-0037 D5) — the state it names lives on the payment
            // axis (Card + PaymentStatus.Pending), which is what the live sweeps read. This generic
            // writer is the only way a new Pending row could appear, so it is refused here rather
            // than by removing the member from Lifecycle, which ranks legacy rows.
            if (command.TargetStatus == OrderStatus.Pending)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.TargetStatus),
                    BusinessErrorMessage.InvalidOrderStatusTransition));
            }

            var currentRank = Array.IndexOf(Lifecycle, currentStatus);
            var targetRank = Array.IndexOf(Lifecycle, command.TargetStatus);

            // A legal override is a strict forward move along the lifecycle. Same-state, backward,
            // and off-lifecycle targets (e.g. Cancelled) are ambiguous and never rewrite history.
            // An unrankable CURRENT status is refused too: unreachable while Lifecycle stays total,
            // which is exactly why it is written down — the next OrderStatus member added and
            // forgotten there would otherwise re-open the backwards move silently.
            if (currentRank < 0 || targetRank < 0 || targetRank <= currentRank)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.TargetStatus),
                    BusinessErrorMessage.InvalidOrderStatusTransition));
            }

            var transition = OrderStatusTrack.Create(command.TargetStatus, order);
            order.AddOrderStatus(transition);

            // The override's FIRST notification-style call (ADR-0029 D2/RV-2): a state card must track
            // admin-driven forward moves. Forward-only, so Cancelled is unreachable here; a target with
            // no activity event (Confirmed) maps to null and produces nothing.
            var eventKey = LiveActivityEventKeys.ForStatus(command.TargetStatus);
            if (eventKey is not null)
            {
                await liveActivityProducer.NotifyOrderTransitionAsync(
                    order, eventKey, transition, cancellationToken);
            }

            auditContext.RecordChange(
                "Order",
                order.Id,
                new StatusSnapshot(order.Id, currentStatus),
                new StatusSnapshot(order.Id, command.TargetStatus));

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                Status: command.TargetStatus));
        }
    }
}
