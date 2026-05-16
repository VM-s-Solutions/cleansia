using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Orders;

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
        IStripeClientFactory stripeClientFactory,
        ILoyaltyService loyaltyService,
        ICancellationPolicyResolver cancellationPolicyResolver,
        IQueueClient queueClient,
        ILogger<Handler> logger
    ) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
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

            if (order.UserId != userId)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

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

            var now = DateTime.UtcNow;
            const bool isFirstTime = false;
            var hasBeenAccepted = order.OrderStatusHistory
                .Any(s => s.Status == OrderStatus.Confirmed);
            var policy = await cancellationPolicyResolver
                .ResolveForUserAsync(userId, cancellationToken);
            var feeRate = BookingPolicy.CalculateCancellationFeeRate(
                order.CleaningDateTime,
                order.CreatedOn.UtcDateTime,
                now,
                isFirstTime,
                hasBeenAccepted,
                freeCancellationHoursOverride: policy.FreeCancellationHours);

            var refundAmount = order.TotalPrice * (1m - feeRate);

            order.Cancel(
                cancelledAtUtc: now,
                cancelledBy: "customer",
                feeRate: feeRate,
                refundAmount: refundAmount,
                reason: command.Reason);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

            var refundInitiated = false;
            if (order.PaymentType == PaymentType.Card
                && order.PaymentStatus == PaymentStatus.Paid
                && refundAmount > 0m
                && !string.IsNullOrEmpty(order.StripeSessionId))
            {
                // Refund try: narrow to StripeException so non-Stripe failures
                // (DI misconfig, null ref) bubble. Refund failure is non-blocking
                // for the cancel itself — the customer's order is still cancelled;
                // the refund just needs manual follow-up.
                try
                {
                    var stripe = stripeClientFactory.CreateClient();
                    await stripe.RefundCheckoutSessionAsync(order.StripeSessionId, refundAmount, cancellationToken);
                    order.UpdatePaymentStatus(PaymentStatus.Refunded);
                    refundInitiated = true;
                }
                catch (StripeException ex)
                {
                    logger.LogError(ex,
                        "Stripe refund failed for order {OrderId}. Manual refund may be required.",
                        order.Id);
                }

                // Notify only if refund actually went through. Push send is in its
                // own try so a transient queue failure doesn't undo the refund or
                // surface as a Stripe-refund-failed log line.
                if (refundInitiated && !string.IsNullOrEmpty(order.UserId))
                {
                    try
                    {
                        await queueClient.SendAsync(
                            QueueNames.NotificationsDispatch,
                            new SendPushNotificationMessage(
                                UserId: order.UserId,
                                EventKey: NotificationEventCatalog.OrderRefunded,
                                Args: new Dictionary<string, string>
                                {
                                    ["orderId"] = order.Id,
                                    ["orderNumber"] = order.DisplayOrderNumber,
                                },
                                TenantId: order.TenantId),
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "Failed to enqueue OrderRefunded push for order {OrderId}; refund itself succeeded.",
                            order.Id);
                    }
                }
            }

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
