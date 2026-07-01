using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
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

[AuditAction("order.refund.full", Sensitive = true, ResourceType = "Order")]
public class AdminRefundOrder
{
    public record Command(
        string OrderId
    ) : ICommand<Response>;

    public record Response(
        string OrderId,
        decimal RefundAmount,
        PaymentStatus PaymentStatus,
        bool RefundInitiated);

    public record RefundSnapshot(
        string OrderId,
        decimal OrderTotal,
        decimal ConsumedRefund,
        PaymentStatus PaymentStatus);

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
        IRefundRepository refundRepository,
        IRefundService refundService,
        IUserSessionProvider userSessionProvider,
        IPendingDispatch pending,
        IAuditContext auditContext
    ) : ICommandHandler<Command, Response>
    {
        // Stable full-refund purpose for the deterministic RefundKey (refund:{OrderId}:admin:full). One
        // per order, so a retried admin refund-only collapses on the same key and never double-refunds.
        private const string FullRefundRequestId = "full";

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

            // Refund-only is a card money-out on a paid order. The lifecycle status is left untouched —
            // cancellation is a separate command (AdminCancelOrder).
            if (order.PaymentType != PaymentType.Card
                || order.PaymentStatus != PaymentStatus.Paid
                || !order.HasRefundableChargeSurface)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.RefundOrderNotRefundable));
            }

            var consumedBefore = await refundRepository.GetSucceededRefundTotalForOrderAsync(
                order.Id, cancellationToken);

            var refund = await refundService.IssueRefundAsync(
                new RefundRequest(
                    order.Id,
                    order.TotalPrice,
                    RefundReason.AdminDiscretion,
                    adminId,
                    RefundRequestId: FullRefundRequestId),
                cancellationToken);

            if (refund.IsFailure)
            {
                return BusinessResult.Failure<Response>(refund.Error!);
            }

            var result = refund.Value!;
            var consumed = await refundRepository.GetSucceededRefundTotalForOrderAsync(
                order.Id, cancellationToken);
            var paymentStatus = consumed >= order.TotalPrice
                ? PaymentStatus.Refunded
                : PaymentStatus.PartiallyRefunded;

            auditContext.RecordChange(
                "Order",
                order.Id,
                new RefundSnapshot(order.Id, order.TotalPrice, consumedBefore, order.PaymentStatus),
                new RefundSnapshot(order.Id, order.TotalPrice, consumed, paymentStatus));

            if (!string.IsNullOrEmpty(order.UserId))
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

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                RefundAmount: result.Amount,
                PaymentStatus: paymentStatus,
                RefundInitiated: true));
        }
    }
}
