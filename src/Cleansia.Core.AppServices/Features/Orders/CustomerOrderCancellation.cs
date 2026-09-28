using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
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
    ICreditAccountRepository creditAccountRepository,
    ILoyaltyService loyaltyService,
    ICancellationPolicyResolver cancellationPolicyResolver,
    INotificationProducer notificationProducer,
    ILiveActivityProducer liveActivityProducer,
    IExpressWaiverConsumer expressWaiverConsumer,
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
        // The refund seam commits its claim and confirmed amount. A guest cancellation stays retryable
        // until its status, audit and email intent can commit together after those independent flushes.
        if (guest)
        {
            await RefundAsync(recoverGuestAttempt: true);
        }

        order.Cancel(now, CancelledBy.Customer, assessment.FeeRate, assessment.RefundAmount, reason);
        var transition = OrderStatusTrack.Create(OrderStatus.Cancelled, order);
        order.AddOrderStatus(transition);
        await liveActivityProducer.NotifyOrderTransitionAsync(
            order, LiveActivityEventKeys.End, transition, cancellationToken);

        if (!guest)
        {
            await RefundAsync(recoverGuestAttempt: false);
        }

        if (paymentStatusAtCancel != PaymentStatus.Paid && !refundInitiated)
        {
            await creditAccountRepository.ReturnUnpaidOrderCreditAsync(order, actorId, cancellationToken);
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
            PolicyFigures: CancellationPolicyFigures.Current(),
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
            var request = new RefundRequest(order.Id, assessment.RefundAmount,
                RefundReason.CustomerCancellation, actorId);
            var existing = recoverGuestAttempt
                ? await refundRepository.GetByRefundKeyAsync(RefundService.BuildRefundKey(request), cancellationToken)
                : null;
            if (existing is null && !(order.PaymentType == PaymentType.Card
                && order.PaymentStatus == PaymentStatus.Paid
                && assessment.RefundAmount > 0m && order.HasRefundableChargeSurface))
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
                await ReturnCreditShareAsync(request);
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

        // The card leg waits for the re-drive; the credit leg comes back now, on the refund's own key, so
        // the re-drive's credit leg finds it already returned.
        async Task ReturnCreditShareAsync(RefundRequest request)
        {
            var alreadyReturned = await creditAccountRepository.GetReturnedTotalForOrderAsync(
                order.Id, cancellationToken);
            var (_, creditShare) = RefundService.SplitAcrossTenders(order, request.Amount, alreadyReturned);
            await creditAccountRepository.ReturnCreditAsync(
                order, creditShare, RefundService.BuildRefundKey(request), actorId, cancellationToken);
        }
    }
}
