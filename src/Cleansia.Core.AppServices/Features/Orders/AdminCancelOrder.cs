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

public class AdminCancelOrder
{
    public record Command(
        string OrderId,
        string? Reason
    ) : ICommand<Response>;

    public record Response(
        string OrderId,
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
        IPendingDispatch pending
    ) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var adminId = userSessionProvider.GetUserId()!;
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

            // No ownership gate: the admin acts on ANY order (the customer path's order.UserId != userId
            // rejection does not apply here). Authorization is the AdminOnly policy on the endpoint.

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

            // Admin intervention is not a customer-fault cancellation — no cancellation fee, full refund.
            var refundAmount = order.TotalPrice;

            order.Cancel(
                cancelledAtUtc: DateTime.UtcNow,
                cancelledBy: CancelledBy.Admin,
                feeRate: 0m,
                refundAmount: refundAmount,
                reason: command.Reason);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

            var refundInitiated = false;
            if (order.PaymentType == PaymentType.Card
                && order.PaymentStatus == PaymentStatus.Paid
                && refundAmount > 0m
                && order.HasRefundableChargeSurface)
            {
                // The cancel-purpose RefundKey (refund:{OrderId}:cancel) is one-per-order, so a retried
                // admin cancel — or a customer cancel of the same order — collapses onto the single refund
                // and never double-refunds (ADR-0006 D3).
                var refund = await refundService.IssueRefundAsync(
                    new RefundRequest(order.Id, refundAmount, RefundReason.CustomerCancellation, adminId),
                    cancellationToken);
                refundInitiated = refund.IsSuccess;

                if (refundInitiated && !string.IsNullOrEmpty(order.UserId))
                {
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
                RefundAmount: refundAmount,
                TotalPrice: order.TotalPrice,
                RefundInitiated: refundInitiated));
        }
    }
}
