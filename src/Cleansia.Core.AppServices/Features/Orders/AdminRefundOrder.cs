using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Queue.Abstractions;
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
        ILoyaltyService loyaltyService,
        IUserSessionProvider userSessionProvider,
        INotificationProducer notificationProducer,
        IOutboxMessageRepository outboxMessageRepository,
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

            var refundRequest = new RefundRequest(
                order.Id,
                order.TotalPrice,
                RefundReason.AdminDiscretion,
                adminId,
                RefundRequestId: FullRefundRequestId);

            // Refund-only is a card money-out on a paid order. The lifecycle status is left untouched —
            // cancellation is a separate command (AdminCancelOrder). Its own settled full refund passes
            // again: the seam answers it with that refund and moves nothing, and it is the only way back to
            // a clawback that failed after the refund had already settled and flipped the order to Refunded.
            if (order.PaymentType != PaymentType.Card
                || (order.PaymentStatus != PaymentStatus.Paid
                    && await refundRepository.GetByRefundKeyAsync(RefundService.BuildRefundKey(refundRequest), cancellationToken)
                        is not { Status: RefundStatus.Succeeded })
                || !order.HasRefundableChargeSurface)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.RefundOrderNotRefundable));
            }

            var consumedBefore = await refundRepository.GetSucceededRefundTotalForOrderAsync(
                order.Id, cancellationToken);

            var refund = await refundService.IssueRefundAsync(refundRequest, cancellationToken);

            if (refund.IsFailure)
            {
                return BusinessResult.Failure<Response>(refund.Error!);
            }

            var result = refund.Value!;
            var consumed = await refundRepository.GetSucceededRefundTotalForOrderAsync(
                order.Id, cancellationToken);
            // Against what the CARD was charged, not the sale — RefundService.CardChargedAmount.
            // `consumed` sums the Refunds table, which is card-only, so an order settled partly in
            // credit could never reach the sale total and would report PartiallyRefunded for a refund
            // that had in fact returned every tender in full.
            var paymentStatus = consumed >= RefundService.CardChargedAmount(order)
                ? PaymentStatus.Refunded
                : PaymentStatus.PartiallyRefunded;

            auditContext.RecordChange(
                "Order",
                order.Id,
                new RefundSnapshot(order.Id, order.TotalPrice, consumedBefore, order.PaymentStatus),
                new RefundSnapshot(order.Id, order.TotalPrice, consumed, paymentStatus));

            // Asked of the outbox, not of whether the seam resolved to an earlier refund: the notice is staged
            // with the clawback, so the clawback failure a re-run exists for lost the notice with it. A notice
            // that did commit is not queued again; a second row on its key would fail the commit.
            if (!string.IsNullOrEmpty(order.UserId)
                && await outboxMessageRepository.GetByQueueAndKeyAsync(
                    QueueNames.NotificationsDispatch,
                    MessageKeys.Push(order.UserId, NotificationEventCatalog.OrderRefunded, result.RefundId),
                    cancellationToken) is null)
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

            // Last, because the clawback flushes the unit of work to collapse a duplicate on its key. A full
            // refund hands it the whole price, so it takes everything earlier refunds left of the earn.
            await loyaltyService.RevokeForRefundAsync(
                order.Id, order.TotalPrice, result.RefundKey, adminId, cancellationToken);

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                RefundAmount: result.Amount,
                PaymentStatus: paymentStatus,
                RefundInitiated: true));
        }
    }
}
