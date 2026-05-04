using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Cancel an existing order. Applies the tiered cancellation fee per
/// <see cref="BookingPolicy"/> and triggers a Stripe refund when applicable.
///
/// Rules (see <see cref="BookingPolicy"/>):
///  - Cannot cancel Completed / already-Cancelled / InProgress orders
///  - Acceptance-aware: if no cleaner has accepted (no Confirmed entry in
///    OrderStatusHistory) the cancellation is free regardless of timing
///  - Once accepted:
///      - 24+ hours before start → full refund
///      - 4–24 hours before start → 25% charge, 75% refund
///      - &lt; 4 hours before start → 50% charge, 50% refund
///  - "Oops window" (15 min for returning, 60 min for first-time) → full refund regardless
/// </summary>
public class CancelOrder
{
    public record Command(
        string OrderId,
        string? Reason,
        string UserId = ""
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
        IStripeClientFactory stripeClientFactory,
        ILoyaltyService loyaltyService,
        ICancellationPolicyResolver cancellationPolicyResolver,
        ILogger<Handler> logger
    ) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
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

            // Ownership check — only the booking's customer can cancel.
            if (!string.IsNullOrEmpty(command.UserId) && order.UserId != command.UserId)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.UserId),
                    BusinessErrorMessage.OrderNotOwnedByUser));
            }

            var latestStatus = order.OrderStatusHistory
                .OrderByDescending(s => s.CreatedOn)
                .FirstOrDefault()?.Status;

            // State checks
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

            // Compute fee rate + refund amount.
            var now = DateTime.UtcNow;
            // TODO(first-time-customer): wire up IsFirstTimeCustomer lookup when OrderRepository exposes it.
            const bool isFirstTime = false;
            // Acceptance signal: a cleaner has taken the order iff an OrderStatusHistory
            // entry of Confirmed exists (set by TakeOrder).
            var hasBeenAccepted = order.OrderStatusHistory
                .Any(s => s.Status == OrderStatus.Confirmed);
            // Resolve cancellation policy — Plus members get a wider free-cancel
            // window. Today (no Plus product live) every call returns the
            // standard BookingPolicy values.
            var policy = await cancellationPolicyResolver
                .ResolveForUserAsync(command.UserId, cancellationToken);
            var feeRate = BookingPolicy.CalculateCancellationFeeRate(
                order.CleaningDateTime,
                order.CreatedOn.UtcDateTime,
                now,
                isFirstTime,
                hasBeenAccepted,
                freeCancellationHoursOverride: policy.FreeCancellationHours);

            var refundAmount = order.TotalPrice * (1m - feeRate);

            // Apply on the entity
            order.Cancel(
                cancelledAtUtc: now,
                cancelledBy: "customer",
                feeRate: feeRate,
                refundAmount: refundAmount,
                reason: command.Reason);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

            // Trigger Stripe refund for card payments if money was taken and we owe a refund.
            var refundInitiated = false;
            if (order.PaymentType == PaymentType.Card
                && order.PaymentStatus == PaymentStatus.Paid
                && refundAmount > 0m
                && !string.IsNullOrEmpty(order.StripeSessionId))
            {
                try
                {
                    var stripe = stripeClientFactory.CreateClient();
                    await stripe.RefundCheckoutSessionAsync(order.StripeSessionId, refundAmount, cancellationToken);
                    order.UpdatePaymentStatus(PaymentStatus.Refunded);
                    refundInitiated = true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Stripe refund failed for order {OrderId}. Manual refund may be required.",
                        order.Id);
                    // Do NOT fail the cancellation — the order is still cancelled, ops will reconcile.
                }
            }

            // Loyalty: revoke any prior tier-point earn for this order.
            // Idempotent — no-op if no Earn ever existed (e.g. cancelling
            // an order before completion) or already revoked.
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
