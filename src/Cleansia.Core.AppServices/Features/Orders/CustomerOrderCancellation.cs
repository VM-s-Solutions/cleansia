using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging;
using static Cleansia.Core.AppServices.Features.Orders.CancelOrder;

namespace Cleansia.Core.AppServices.Features.Orders;

public sealed class CustomerOrderCancellation(
    ITenantProvider tenantProvider,
    IRefundService refundService,
    IRefundRepository refundRepository,
    IReceivableRepository receivableRepository,
    ICreditAccountRepository creditAccountRepository,
    ILoyaltyService loyaltyService,
    ICancellationPolicyResolver cancellationPolicyResolver,
    INotificationProducer notificationProducer,
    ILiveActivityProducer liveActivityProducer,
    IExpressWaiverConsumer expressWaiverConsumer,
    IPendingDispatch pending,
    IAuditContext auditContext,
    TimeProvider timeProvider,
    ILogger<CustomerOrderCancellation> logger)
{
    public record Result(Response Response, decimal? SuccessfulRefundAmount);

    public string? BlockedReason(Order order) =>
        CancellationAssessor.BlockedReason(order, timeProvider.GetUtcNow().UtcDateTime);

    public async Task<Result> ExecuteAsync(
        Order order, string? reason, string actorId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var policy = await cancellationPolicyResolver.ResolveForOrderAsync(order, cancellationToken);
        var assessment = CancellationAssessor.Assess(order, policy, now);
        var paymentStatusAtCancel = order.PaymentStatus;
        if (!string.IsNullOrEmpty(order.TenantId))
        {
            tenantProvider.SetTenantOverride(order.TenantId);
        }

        var guest = string.IsNullOrEmpty(order.UserId);
        var refundInitiated = false;
        var refundPending = false;
        decimal? successfulRefundAmount = null;
        var refundKey = RefundService.BuildRefundKey(
            new RefundRequest(order.Id, 0m, RefundReason.CustomerCancellation, actorId));
        var cancellationRefund = assessment.RefundAmount;
        if (!order.TookNoPayment)
        {
            // The fee is a share of the price, so what comes back is what the sale still holds less the fee
            // (owner ruling 2026-10-06). A pending refund's credit leg waits for its card and is not returned
            // yet, so it is counted here at the proportion the seam reads such a leg back with.
            var left = await RefundService.LeftToGiveBackAsync(
                refundRepository, creditAccountRepository, order, refundKey, cancellationToken);
            var cardCharged = RefundService.CardChargedAmount(order);
            var pendingCredit = assessment.FeeRate > 0m && cardCharged > 0m
                ? Math.Round(
                    await refundRepository.GetPendingRefundTotalForOrderAsync(order.Id, refundKey, cancellationToken)
                    * order.CreditAppliedAmount / cardCharged,
                    2,
                    MidpointRounding.AwayFromZero)
                : 0m;
            cancellationRefund = Math.Max(0m, left - pendingCredit - assessment.FeeAmount);

            // A guest's own cancel that never committed left its claim on this key, and a retry re-sends that
            // claim at its own amount, so that is what is recorded, as the platform replay does.
            if (await refundRepository.GetByRefundKeyAsync(refundKey, cancellationToken) is { } claimed)
            {
                cancellationRefund = Math.Min(left,
                    claimed.Amount + await creditAccountRepository.GetReturnedForRefundAsync(refundKey, cancellationToken));
            }
        }

        var request = new RefundRequest(
            order.Id,
            assessment.FeeRate == 0m ? assessment.RefundAmount : cancellationRefund,
            RefundReason.CustomerCancellation,
            actorId);

        // The refund seam commits its claim and confirmed amount. A guest cancellation stays retryable
        // until its status, audit and email intent can commit together after those independent flushes.
        if (guest)
        {
            await RefundAsync(recoverGuestAttempt: true);
        }

        order.Cancel(now, CancelledBy.Customer, assessment.FeeRate, cancellationRefund, reason);
        var transition = OrderStatusTrack.Create(OrderStatus.Cancelled, order);
        order.AddOrderStatus(transition);
        await liveActivityProducer.NotifyOrderTransitionAsync(
            order, LiveActivityEventKeys.End, transition, cancellationToken);

        if (!guest && order.PaymentType == PaymentType.Cash && order.TookNoPayment && assessment.FeeAmount > 0m)
        {
            receivableRepository.Add(Receivable.ForCashCancellationFee(order, assessment.FeeAmount));
        }

        if (!guest)
        {
            await RefundAsync(recoverGuestAttempt: false);
        }

        if (paymentStatusAtCancel != PaymentStatus.Paid && !refundInitiated && !refundPending
            && (order.TookNoPayment || assessment.FeeRate == 0m))
        {
            await creditAccountRepository.ReturnUnpaidOrderCreditAsync(
                order,
                await RefundService.CardRefundedOrOwedAsync(refundRepository, order.Id, null, cancellationToken),
                actorId,
                cancellationToken);
        }

        if (!order.TookNoPayment && assessment.FeeAmount > 0m)
        {
            CalculateOrderPay.EnqueueForCrew(order, pending);
        }

        var waiverReleased = !assessment.HasBeenAccepted
            && await expressWaiverConsumer.ReleaseForOrderAsync(order.Id, cancellationToken);
        await OrderAssignmentCancellationNotifier.NotifyAssignedEmployeesOfCancellationAsync(
            order, notificationProducer, cancellationToken);
        await loyaltyService.RevokeForCancelledOrderAsync(order.Id, cancellationToken);

        var refundAmount = order.CancellationRefundAmount ?? 0m;
        auditContext.RecordEvidence("Order", order.Id, new OrderCancellationEvidence(
            Tier: assessment.Tier,
            FeeRate: assessment.FeeRate,
            FeeAmount: assessment.FeeAmount,
            RefundAmount: refundAmount,
            TotalPrice: order.TotalPrice,
            CurrencyId: order.CurrencyId,
            HasBeenAccepted: assessment.HasBeenAccepted,
            HoursBeforeCleaning: Math.Round((decimal)(order.CleaningDateTime - now).TotalHours, 2),
            MinutesSinceBooking: Math.Round((decimal)(now - order.CreatedOn.UtcDateTime).TotalMinutes, 2),
            FreeCancellationHoursApplied: policy.FreeCancellationHours,
            OopsMinutesApplied: policy.OopsWindowMinutes,
            OopsRuleApplied: policy.OopsWindowRule,
            PolicyFigures: CancellationPolicyFigures.Of(order),
            ExpressWaiverReleased: waiverReleased,
            RefundInitiated: refundInitiated,
            PaymentType: order.PaymentType,
            PaymentStatus: paymentStatusAtCancel,
            ReasonProvided: !string.IsNullOrWhiteSpace(reason),
            ActualRefundAmount: successfulRefundAmount));

        return new Result(new Response(order.Id, assessment.FeeRate, refundAmount,
            order.TotalPrice, refundInitiated, successfulRefundAmount, refundPending), successfulRefundAmount);

        async Task RefundAsync(bool recoverGuestAttempt)
        {
            var existing = recoverGuestAttempt
                ? await refundRepository.GetByRefundKeyAsync(refundKey, cancellationToken)
                : null;
            // A free cancellation of a partly refunded order asks for the price; the seam holds it to what the
            // sale has left, on each tender.
            if (existing is null && !(order.PaymentType == PaymentType.Card
                && (order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.PartiallyRefunded)
                && request.Amount > 0m && order.HasRefundableChargeSurface))
            {
                return;
            }

            BusinessResult<RefundResult>? refund;
            try
            {
                refund = await refundService.IssueRefundAsync(request, cancellationToken);
            }
            // A guest's attempt stays retryable by escaping; a member's cancel has already committed, so
            // the refund is left pending for the re-drive, like the unfilled sweep's.
            catch (Exception ex) when (!guest && RefundService.IsStripeTransportFailure(ex, cancellationToken))
            {
                logger.LogError(ex,
                    "Could not reach Stripe to refund cancelled order {OrderId}; the refund is left pending",
                    order.Id);
                refund = null;
            }

            refundInitiated = refund is { IsSuccess: true };
            successfulRefundAmount = refundInitiated ? refund!.Value?.Amount : null;
            if (!refundInitiated && !guest
                && (refund is null || refund.Error?.Message == BusinessErrorMessage.RefundFailed))
            {
                refundPending = true;
                await creditAccountRepository.ReturnPendingRefundCreditLegAsync(
                    refundRepository, order, request, cancellationToken);
            }

            if (refundInitiated && !guest)
            {
                await notificationProducer.NotifyAsync(order.UserId!, NotificationEventCatalog.OrderRefunded,
                    new Dictionary<string, string>
                    {
                        ["orderId"] = order.Id,
                        ["orderNumber"] = order.DisplayOrderNumber,
                    }, order.TenantId, order.Id, cancellationToken);
            }
        }
    }
}
