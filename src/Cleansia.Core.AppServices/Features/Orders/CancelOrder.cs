using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class CancelOrder
{
    public record Command(
        string OrderId,
        string? Reason
    ) : ICommand<Response>;

    public record Response(
        string OrderId,
        decimal FeeRate,
        decimal RefundAmount,
        decimal TotalPrice,
        bool RefundInitiated);

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

            RuleFor(x => x.Reason)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider,
        IRefundService refundService,
        ILoyaltyService loyaltyService,
        ICancellationPolicyResolver cancellationPolicyResolver,
        IPendingDispatch pending
    ) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
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

            if (order.UserId != userId)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            var latestStatus = order.OrderStatusHistory
                .OrderByDescending(s => s.CreatedOn)
                .FirstOrDefault()?.Status;

            if (latestStatus == OrderStatus.Cancelled)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderAlreadyCancelled));
            }
            if (latestStatus == OrderStatus.Completed)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderAlreadyCompleted));
            }
            if (latestStatus == OrderStatus.InProgress)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderInProgressCannotCancel));
            }

            var now = DateTime.UtcNow;
            const bool isFirstTime = false;
            var hasBeenAccepted = order.OrderStatusHistory
                .Any(s => s.Status == OrderStatus.Confirmed);
            var policy = await cancellationPolicyResolver
                .ResolveForUserAsync(userId, cancellationToken);
            var feeRate = BookingPolicy.CalculateCancellationFeeRate(
                order.CleaningDateTime,
                order.CreatedOn.UtcDateTime,
                now,
                isFirstTime,
                hasBeenAccepted,
                freeCancellationHoursOverride: policy.FreeCancellationHours);

            var refundAmount = order.TotalPrice * (1m - feeRate);

            order.Cancel(
                cancelledAtUtc: now,
                cancelledBy: CancelledBy.Customer,
                feeRate: feeRate,
                refundAmount: refundAmount,
                reason: command.Reason);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

            var refundInitiated = false;
            if (order.PaymentType == PaymentType.Card
                && order.PaymentStatus == PaymentStatus.Paid
                && refundAmount > 0m
                && !string.IsNullOrEmpty(order.StripeSessionId))
            {
                var refund = await refundService.IssueRefundAsync(
                    new RefundRequest(order.Id, refundAmount, RefundReason.CustomerCancellation, userId),
                    cancellationToken);
                refundInitiated = refund.IsSuccess;

                if (refundInitiated && !string.IsNullOrEmpty(order.UserId))
                {
                    // ADR-0002: record intent (in-memory, infallible) — the actual send happens
                    // post-commit in PostCommitDispatchBehavior and its failures are logged+swallowed there.
                    pending.Enqueue(
                        QueueNames.NotificationsDispatch,
                        new QueueEnvelope<SendPushNotificationMessage>(
                            MessageKeys.Push(order.UserId, NotificationEventCatalog.OrderRefunded, order.Id),
                            order.TenantId,
                            new SendPushNotificationMessage(
                                UserId: order.UserId,
                                EventKey: NotificationEventCatalog.OrderRefunded,
                                Args: new Dictionary<string, string>
                                {
                                    ["orderId"] = order.Id,
                                    ["orderNumber"] = order.DisplayOrderNumber,
                                },
                                TenantId: order.TenantId)),
                        MessageKeys.Push(order.UserId, NotificationEventCatalog.OrderRefunded, order.Id));
                }
            }

            await loyaltyService.RevokeForCancelledOrderAsync(order.Id, cancellationToken);

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                FeeRate: feeRate,
                RefundAmount: refundAmount,
                TotalPrice: order.TotalPrice,
                RefundInitiated: refundInitiated));
        }
    }
}
