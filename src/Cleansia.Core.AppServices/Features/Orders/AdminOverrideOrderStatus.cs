using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class AdminOverrideOrderStatus
{
    public record Command(
        string OrderId,
        OrderStatus TargetStatus
    ) : ICommand<Response>;

    public record Response(
        string OrderId,
        OrderStatus Status);

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
        IUserSessionProvider userSessionProvider
    ) : ICommandHandler<Command, Response>
    {
        // The forward-only lifecycle the override may walk. Cancelled is intentionally absent —
        // cancellation is AdminCancelOrder (it carries the refund seam); the override is not a
        // back-door into the terminal Cancelled state.
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

            var currentStatus = order.OrderStatusHistory
                .OrderByDescending(s => s.CreatedOn)
                .FirstOrDefault()?.Status;

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

            var currentRank = Array.IndexOf(Lifecycle, currentStatus ?? OrderStatus.New);
            var targetRank = Array.IndexOf(Lifecycle, command.TargetStatus);

            // A legal override is a strict forward move along the lifecycle. Same-state, backward,
            // and off-lifecycle targets (e.g. Cancelled) are ambiguous and never rewrite history.
            if (targetRank < 0 || targetRank <= currentRank)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.TargetStatus),
                    BusinessErrorMessage.InvalidOrderStatusTransition));
            }

            order.AddOrderStatus(OrderStatusTrack.Create(command.TargetStatus, order));

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                Status: command.TargetStatus));
        }
    }
}
