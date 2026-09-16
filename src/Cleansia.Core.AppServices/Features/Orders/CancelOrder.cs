using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.AppServices.Services;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

[AuditAction("customer.order.cancel", Audience = AuditAudience.Customer, ResourceType = "Order")]
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

    /// <summary>
    /// What the cancel cost and why, as the server computed it at the click (ADR-0062 D3). The reason
    /// text stays on <c>Order.CancellationReason</c>; the row records only that one was given.
    /// </summary>
    public record OrderCancellationEvidence(
        CancellationFeeTier Tier,
        decimal FeeRate,
        decimal FeeAmount,
        decimal RefundAmount,
        decimal TotalPrice,
        string CurrencyId,
        bool HasBeenAccepted,
        decimal HoursBeforeCleaning,
        decimal MinutesSinceBooking,
        int FreeCancellationHoursApplied,
        CancellationPolicyFigures PolicyFigures,
        bool ExpressWaiverReleased,
        bool RefundInitiated,
        PaymentType PaymentType,
        PaymentStatus PaymentStatus,
        bool ReasonProvided) : ICustomerAuditPayload;

    public record CancellationPolicyFigures(
        int FreeHours,
        int PartialHours,
        decimal PartialRate,
        decimal LastMinuteRate,
        int OopsMinutesStandard,
        int OopsMinutesFirstTime)
    {
        public static CancellationPolicyFigures Current() => new(
            BookingPolicy.FreeCancellationHours,
            BookingPolicy.PartialCancellationHours,
            BookingPolicy.PartialCancellationFeeRate,
            BookingPolicy.LastMinuteCancellationFeeRate,
            BookingPolicy.OopsWindowMinutesStandard,
            BookingPolicy.OopsWindowMinutesFirstTime);
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.Reason)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider,
        ITenantProvider tenantProvider,
        IRefundService refundService,
        ICreditAccountRepository creditAccountRepository,
        ILoyaltyService loyaltyService,
        ICancellationPolicyResolver cancellationPolicyResolver,
        INotificationProducer notificationProducer,
        ILiveActivityProducer liveActivityProducer,
        IExpressWaiverConsumer expressWaiverConsumer,
        IAuditContext auditContext
    ) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            // Missing and foreign orders share the handler's refusal and failure-audit path.
            var order = await orderAccessService
                .OrdersForCaller()
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

            if (order.UserId != userId)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            if (CancellationAssessor.BlockedReason(order) is { } blockedReason)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    blockedReason));
            }

            var now = DateTime.UtcNow;
            var policy = await cancellationPolicyResolver
                .ResolveForUserAsync(userId, cancellationToken);
            var assessment = CancellationAssessor.Assess(order, policy, now);
            var feeRate = assessment.FeeRate;
            var refundAmount = assessment.RefundAmount;
            var paymentStatusAtCancel = order.PaymentStatus;

            // From here the request acts as the ORDER's operator, which for a booking made across the
            // border is not the customer's own company: the status row, the refund (which commits
            // mid-flight, on its own), and this act's audit row all belong in the market's books. The
            // policy above was read first because it is the customer's own membership. Loyalty and credit
            // below follow the account and read past the filter by the customer's id.
            if (!string.IsNullOrEmpty(order.TenantId))
            {
                tenantProvider.SetTenantOverride(order.TenantId);
            }

            order.Cancel(
                cancelledAtUtc: now,
                cancelledBy: CancelledBy.Customer,
                feeRate: feeRate,
                refundAmount: refundAmount,
                reason: command.Reason);
            var transition = OrderStatusTrack.Create(OrderStatus.Cancelled, order);
            order.AddOrderStatus(transition);

            // Unconditional, beside the append — the customer alert on cancel is refund-conditional
            // and must NOT gate the activity end-push (a dead order must leave the lock screen).
            await liveActivityProducer.NotifyOrderTransitionAsync(
                order, LiveActivityEventKeys.End, transition, cancellationToken);

            var refundInitiated = false;
            if (order.PaymentType == PaymentType.Card
                && order.PaymentStatus == PaymentStatus.Paid
                && refundAmount > 0m
                && order.HasRefundableChargeSurface)
            {
                var refund = await refundService.IssueRefundAsync(
                    new RefundRequest(order.Id, refundAmount, RefundReason.CustomerCancellation, userId),
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
                        order.TenantId,
                        order.Id,
                        cancellationToken);
                }
            }
            else if (order.PaymentStatus != PaymentStatus.Paid)
            {
                // The card was never charged, so there is nothing to refund - but credit WAS taken at
                // checkout, and it is the only money the customer has actually paid. All of it comes
                // back; the fee the assessor computed is unrecoverable on an unpaid order either way.
                // -> CreditUnwind.ReturnUnpaidOrderCreditAsync
                await creditAccountRepository.ReturnUnpaidOrderCreditAsync(order, userId, cancellationToken);
            }

            // Release the express waiver iff no cleaner was ever pulled onto this short-notice job —
            // the SAME assignment predicate the fee above uses, and for the same reason: nothing was
            // consumed, so the member keeps their credit. With an assignment the credit is spent, which
            // is what bounds the book → cleaner accepts → cancel → repeat loop at two attempts a month.
            // Deliberately keyed on the order's own state, not on CancelledBy: both system sweeps append
            // a status track without calling Order.Cancel, so their orders release here with no change to
            // either sweep.
            var expressWaiverReleased = false;
            if (!assessment.HasBeenAccepted)
            {
                expressWaiverReleased = await expressWaiverConsumer.ReleaseForOrderAsync(order.Id, cancellationToken);
            }

            // Tell every cleaner who ACCEPTED this job that it's off — they hear nothing today.
            // Distinct partner event (not the customer order.cancelled) so the audience keysets stay
            // disjoint; skips legacy assignments with no linked User.
            await OrderAssignmentCancellationNotifier.NotifyAssignedEmployeesOfCancellationAsync(
                order, notificationProducer, cancellationToken);

            await loyaltyService.RevokeForCancelledOrderAsync(order.Id, cancellationToken);

            auditContext.RecordEvidence("Order", order.Id, new OrderCancellationEvidence(
                Tier: assessment.Tier,
                FeeRate: feeRate,
                FeeAmount: assessment.FeeAmount,
                RefundAmount: refundAmount,
                TotalPrice: order.TotalPrice,
                CurrencyId: order.CurrencyId,
                HasBeenAccepted: assessment.HasBeenAccepted,
                HoursBeforeCleaning: Math.Round((decimal)(order.CleaningDateTime - now).TotalHours, 2),
                MinutesSinceBooking: Math.Round((decimal)(now - order.CreatedOn.UtcDateTime).TotalMinutes, 2),
                FreeCancellationHoursApplied: policy.FreeCancellationHours,
                PolicyFigures: CancellationPolicyFigures.Current(),
                ExpressWaiverReleased: expressWaiverReleased,
                RefundInitiated: refundInitiated,
                PaymentType: order.PaymentType,
                PaymentStatus: paymentStatusAtCancel,
                ReasonProvided: !string.IsNullOrWhiteSpace(command.Reason)));

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                FeeRate: feeRate,
                RefundAmount: refundAmount,
                TotalPrice: order.TotalPrice,
                RefundInitiated: refundInitiated));
        }
    }
}
