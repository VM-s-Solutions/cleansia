using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
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
        INotificationProducer notificationProducer,
        ILiveActivityProducer liveActivityProducer,
        IExpressWaiverConsumer expressWaiverConsumer
    ) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var adminId = userSessionProvider.GetUserId()!;
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees)
                    .ThenInclude(ae => ae.Employee)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            // No ownership gate: the admin acts on ANY order (the customer path's order.UserId != userId
            // rejection does not apply here). Authorization is the AdminOnly policy on the endpoint.

            var latestStatus = order.CurrentStatus;

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
            var transition = OrderStatusTrack.Create(OrderStatus.Cancelled, order);
            order.AddOrderStatus(transition);

            // Unconditional, beside the append — the refund-conditional customer alert must not gate
            // the activity end-push (mirrors CancelOrder).
            await liveActivityProducer.NotifyOrderTransitionAsync(
                order, LiveActivityEventKeys.End, transition, cancellationToken);

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
                    await notificationProducer.NotifyAsync(
                        order.UserId,
                        NotificationEventCatalog.OrderRefunded,
                        new Dictionary<string, string>
                        {
                            ["orderId"] = order.Id,
                            ["orderNumber"] = order.DisplayOrderNumber,
                        },
                        // The dedup subject is the REFUND, not the order. Three handlers raise this one
                        // event — an admin refund, an admin cancellation that refunds, and a dispute
                        // resolved with a refund — and all three keyed it on the order, so the second
                        // refund an order ever saw minted a key the first had written. The outbox's unique
                        // index raises that at the pipeline's commit, AFTER the Stripe refund has already
                        // settled: the money left, the transaction rolled back, and the customer was never
                        // told. RefundResult.RefundId is stable per refund and the service already resolves
                        // a repeat to the existing one, so a genuinely duplicate notice still collapses.
                        order.TenantId,
                        refund.Value!.RefundId,
                        cancellationToken);
                }
            }

            // Unconditionally, even with a cleaner assigned: an admin cancellation is OUR action, not the
            // customer's, so charging the member's monthly perk for it would be unfair with information
            // already in hand.
            await expressWaiverConsumer.ReleaseForOrderAsync(order.Id, cancellationToken);

            // Every cleaner who accepted this job is told it's off (partner-targeted event; skips
            // legacy assignments with no linked User) — mirrors the customer CancelOrder path.
            await OrderAssignmentCancellationNotifier.NotifyAssignedEmployeesOfCancellationAsync(
                order, notificationProducer, cancellationToken);

            await loyaltyService.RevokeForCancelledOrderAsync(order.Id, cancellationToken);

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                RefundAmount: refundAmount,
                TotalPrice: order.TotalPrice,
                RefundInitiated: refundInitiated));
        }
    }
}
