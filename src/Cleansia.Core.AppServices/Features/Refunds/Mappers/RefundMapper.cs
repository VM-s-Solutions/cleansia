using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Refunds.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;

namespace Cleansia.Core.AppServices.Features.Refunds.Mappers;

public static class RefundMapper
{
    public static RefundListItem MapToListItem(this Refund refund) =>
        new(
            Id: refund.Id,
            OrderId: refund.OrderId,
            DisplayOrderNumber: refund.Order!.DisplayOrderNumber,
            DisputeId: refund.DisputeId,
            UserId: refund.Order.UserId,
            CustomerName: refund.Order.CustomerName,
            HasAccount: refund.Order.UserId is not null,
            Amount: refund.Amount,
            CurrencyCode: refund.Currency,
            Origin: refund.MapToOrigin().MapToCode(),
            State: refund.MapToState().MapToCode(),
            RequestedOn: refund.CreatedOn,
            LastChangedOn: refund.UpdatedOn ?? refund.CreatedOn,
            RepaidOn: refund.RepaidOn,
            RepaymentMethod: refund.RepaymentMethod?.MapToCode(),
            RepaymentReference: refund.RepaymentReference,
            StripeRefundId: refund.StripeRefundId,
            CanRetry: refund.CanRetry());

    /// <summary>
    /// A failed date tells money still owed (Failed, Bounced) from a refund that had nothing left to give back
    /// (Closed); a repaid date ends both. → /flows/cancellation-refund-dispute#refund
    /// </summary>
    public static RefundState MapToState(this Refund refund) => refund switch
    {
        { RepaidOn: not null } => RefundState.Repaid,
        { Status: RefundStatus.Pending } => RefundState.NotSent,
        { Status: RefundStatus.Succeeded, FailedOn: null } => RefundState.Refunded,
        { Status: RefundStatus.Succeeded } => RefundState.Bounced,
        { FailedOn: not null } => RefundState.Failed,
        _ => RefundState.Closed,
    };

    /// <summary>A cancellation's refund is the key its reason builds with no distinguishing id, as the re-drive reads it.</summary>
    public static RefundOrigin MapToOrigin(this Refund refund)
    {
        if (refund.DisputeId is not null)
        {
            return RefundOrigin.Dispute;
        }

        if (refund.RefundKey == RefundService.BuildRefundKey(new RefundRequest(refund.OrderId, 0m, refund.Reason, string.Empty)))
        {
            return RefundOrigin.Cancellation;
        }

        return refund.RefundKey == RefundService.BuildRefundKey(new RefundRequest(
                refund.OrderId, 0m, RefundReason.AdminDiscretion, string.Empty, RefundRequestId: AdminRefundOrder.Handler.FullRefundRequestId))
            ? RefundOrigin.FullRefund
            : RefundOrigin.PartialRefund;
    }

    /// <summary>
    /// A dispute's refund is retried by resolving the dispute again; a cancellation's only once its order is
    /// cancelled, since a guest's refund is claimed before a cancel that may have failed.
    /// </summary>
    public static bool CanRetry(this Refund refund) =>
        refund.MapToState() == RefundState.NotSent
        && refund.MapToOrigin() switch
        {
            RefundOrigin.Dispute => false,
            RefundOrigin.Cancellation => refund.Order!.CurrentStatus == OrderStatus.Cancelled,
            _ => true,
        };
}
