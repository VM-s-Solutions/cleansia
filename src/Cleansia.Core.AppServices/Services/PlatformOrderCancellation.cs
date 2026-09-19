using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.Core.AppServices.Services;

public sealed class PlatformOrderCancellation(
    IRefundService refundService,
    ICreditAccountRepository creditAccountRepository,
    ILoyaltyService loyaltyService,
    INotificationProducer notificationProducer,
    ILiveActivityProducer liveActivityProducer,
    IExpressWaiverConsumer expressWaiverConsumer) : IPlatformOrderCancellation
{
    public async Task<PlatformOrderCancellationResult> CancelAsync(
        Order order,
        string actorId,
        CancelledBy cancelledBy,
        string? reason,
        RefundReason refundReason,
        CancellationToken cancellationToken)
    {
        // A platform cancellation is not a customer-fault cancellation — no cancellation fee, full refund.
        var refundAmount = order.TotalPrice;

        order.Cancel(
            cancelledAtUtc: DateTime.UtcNow,
            cancelledBy: cancelledBy,
            feeRate: 0m,
            refundAmount: refundAmount,
            reason: reason);
        var transition = OrderStatusTrack.Create(OrderStatus.Cancelled, order);
        order.AddOrderStatus(transition);

        // Unconditional, beside the append — the refund-conditional customer alert must not gate
        // the activity end-push (mirrors CancelOrder).
        await liveActivityProducer.NotifyOrderTransitionAsync(
            order, LiveActivityEventKeys.End, transition, cancellationToken);

        var refund = PlatformRefundOutcome.NotAttempted;
        if (order.PaymentType == PaymentType.Card
            && order.PaymentStatus == PaymentStatus.Paid
            && refundAmount > 0m
            && order.HasRefundableChargeSurface)
        {
            refund = await RefundAsync(order, actorId, refundReason, cancellationToken);
        }
        else if (order.PaymentStatus != PaymentStatus.Paid)
        {
            // The card was never charged, so there is nothing to refund - but credit WAS taken at
            // checkout and is the only money this customer actually paid. A platform cancellation is
            // fee-free anyway, so all of it comes back.
            await creditAccountRepository.ReturnUnpaidOrderCreditAsync(order, actorId, cancellationToken);
        }

        // Unconditionally, even with a cleaner assigned: a platform cancellation is OUR action, not the
        // customer's, so charging the member's monthly perk for it would be unfair with information
        // already in hand.
        await expressWaiverConsumer.ReleaseForOrderAsync(order.Id, cancellationToken);

        // Every cleaner who accepted this job is told it's off (partner-targeted event; skips
        // legacy assignments with no linked User) — mirrors the customer CancelOrder path.
        await OrderAssignmentCancellationNotifier.NotifyAssignedEmployeesOfCancellationAsync(
            order, notificationProducer, cancellationToken);

        await loyaltyService.RevokeForCancelledOrderAsync(order.Id, cancellationToken);

        return new PlatformOrderCancellationResult(refundAmount, refund);
    }

    public async Task<PlatformRefundOutcome> RefundAsync(
        Order order,
        string actorId,
        RefundReason refundReason,
        CancellationToken cancellationToken)
    {
        // The refund key is derived from the reason and is one-per-order per purpose, so a retried
        // cancel — or a customer cancel of the same order — collapses onto the single refund and never
        // double-refunds (ADR-0006 D3).
        var refund = await refundService.IssueRefundAsync(
            new RefundRequest(order.Id, order.TotalPrice, refundReason, actorId),
            cancellationToken);

        if (refund.IsFailure)
        {
            return PlatformRefundOutcome.Failed(refund.Error?.Message);
        }

        if (!string.IsNullOrEmpty(order.UserId))
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

        return PlatformRefundOutcome.Issued;
    }
}
