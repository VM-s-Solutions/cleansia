using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Payments;

public class HandlePaymentNotification
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IStripeConfig _stripeConfig;
        private readonly IOrderRepository _orderRepository;

        public Validator(IStripeConfig stripeConfig, IOrderRepository orderRepository)
        {
            _stripeConfig = stripeConfig;
            _orderRepository = orderRepository;

            RuleFor(x => x.JsonPayload)
                .NotEmpty().WithMessage(BusinessErrorMessage.JsonPayloadRequired);

            RuleFor(x => x.SignatureHeader)
                .NotEmpty().WithMessage(BusinessErrorMessage.StripeSignatureRequired);

            RuleFor(x => x)
                .MustAsync(OrderExistsAsync)
                .When(NotificationIsHandled);
        }

        private bool NotificationIsHandled(Command command)
        {
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    command.JsonPayload, command.SignatureHeader, _stripeConfig.WebhookSecret,
                    throwOnApiVersionMismatch: false);
                return Constants.StripeEventType.IsOrderEvent(stripeEvent.Type)
                    || Constants.StripeEventType.IsSubscriptionEvent(stripeEvent.Type);
            }
            catch (StripeException)
            {
                return false; // Invalid signature — skip validation, handler will return proper error
            }
        }

        private async Task<bool> OrderExistsAsync(Command command, CancellationToken cancellationToken)
        {
            var stripeEvent = EventUtility.ConstructEvent(
                command.JsonPayload, command.SignatureHeader, _stripeConfig.WebhookSecret,
                throwOnApiVersionMismatch: false);

            // Subscription events don't reference an order; the order-existence
            // check is order-flow only. The handler does its own subscription-
            // existence check (warns + no-ops if the local row is missing).
            if (Constants.StripeEventType.IsSubscriptionEvent(stripeEvent.Type))
            {
                return true;
            }

            var orderId = ExtractOrderId(stripeEvent);
            if (orderId is null)
            {
                return true; // Unhandled event type — no order check needed
            }

            return !string.IsNullOrWhiteSpace(orderId)
                && await _orderRepository.ExistsAsync(orderId, cancellationToken);
        }
    }

    /// <summary>
    /// Pulls the OrderId metadata off the Stripe payload, regardless of
    /// whether it's a Checkout Session (web) or PaymentIntent (mobile).
    /// Returns null for event types that don't carry an OrderId.
    /// </summary>
    private static string? ExtractOrderId(Event stripeEvent)
    {
        if (stripeEvent.Type is Constants.StripeEventType.CompletedSession
                             or Constants.StripeEventType.ExpiredSession)
        {
            var session = stripeEvent.Data.Object as Session;
            return session?.Metadata?.GetValueOrDefault("OrderId");
        }
        if (stripeEvent.Type is Constants.StripeEventType.PaymentIntentSucceeded
                             or Constants.StripeEventType.PaymentIntentPaymentFailed
                             or Constants.StripeEventType.PaymentIntentCanceled)
        {
            var intent = stripeEvent.Data.Object as PaymentIntent;
            return intent?.Metadata?.GetValueOrDefault("OrderId");
        }
        return null;
    }

    public record Command(string JsonPayload, string SignatureHeader, string Language = Constants.Language.English) : ICommand<string>;

    public class Handler(
        IStripeConfig stripeConfig,
        IOrderRepository orderRepository,
        IProcessedStripeEventRepository processedStripeEventRepository,
        IStripeSubscriptionWebhookHandler subscriptionWebhookHandler,
        ITenantProvider tenantProvider,
        IQueueClient queueClient,
        ILogger<Handler> logger) : ICommandHandler<Command, string>
    {
        public async Task<BusinessResult<string>> Handle(Command command, CancellationToken cancellationToken)
        {
            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    command.JsonPayload, command.SignatureHeader, stripeConfig.WebhookSecret,
                    throwOnApiVersionMismatch: false);
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Invalid webhook signature");
                return BusinessResult.Failure<string>(new Error(
                    "InvalidSignature",
                    "Invalid webhook signature"));
            }

            // Idempotency gate. Stripe retries on socket timeout / 5xx, and
            // a slow first delivery can overlap with a retry. The
            // ProcessedStripeEvents table has a UNIQUE index on StripeEventId;
            // the sequential-retry case is caught here, and the rare
            // parallel-retry case is caught by the index at commit time
            // (one INSERT wins, the other gets a DbUpdateException which
            // bubbles up, Stripe retries, the next try sees the row and
            // short-circuits). Either way, side effects fire at most once.
            if (await processedStripeEventRepository.HasProcessedAsync(stripeEvent.Id, cancellationToken))
            {
                logger.LogInformation(
                    "Stripe event {EventId} ({EventType}) already processed; short-circuiting",
                    stripeEvent.Id, stripeEvent.Type);
                return BusinessResult.Success(stripeEvent.Id);
            }

            // Stamp this event as processed BEFORE any side effects. The row
            // is committed atomically with the rest of the handler's work via
            // the UnitOfWork pipeline — if anything below throws, the stamp
            // rolls back too and Stripe's retry will re-run the handler.
            processedStripeEventRepository.Add(ProcessedStripeEvent.Create(
                stripeEventId: stripeEvent.Id,
                eventType: stripeEvent.Type,
                stripeCreatedAt: stripeEvent.Created));

            // Subscription lifecycle (Cleansia Plus) — handle separately, no
            // order to look up. The local UserMembership row is the target;
            // resolution happens inside the subscription webhook handler.
            if (Constants.StripeEventType.IsSubscriptionEvent(stripeEvent.Type))
            {
                var subscriptionId = await subscriptionWebhookHandler.HandleAsync(stripeEvent, cancellationToken);
                return BusinessResult.Success(subscriptionId);
            }

            var orderId = ExtractOrderId(stripeEvent);
            if (orderId is null)
            {
                logger.LogInformation("Received webhook event type {EventType}, ignoring", stripeEvent.Type);
                return BusinessResult.Success(string.Empty);
            }
            if (string.IsNullOrEmpty(orderId))
            {
                logger.LogError("Order ID not found in webhook metadata for event type {EventType}", stripeEvent.Type);
                return BusinessResult.Failure<string>(new Error(
                    "OrderIdMissing",
                    "Order ID not found in webhook metadata"));
            }

            var order = await orderRepository.GetByIdIgnoringTenantAsync(orderId, cancellationToken);
            if (order == null)
            {
                logger.LogError("Order {OrderId} not found", orderId);
                return BusinessResult.Failure<string>(new Error(
                    nameof(orderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            if (!string.IsNullOrEmpty(order.TenantId))
            {
                tenantProvider.SetTenantOverride(order.TenantId);
            }

            // Dispatch by event type. Checkout Session events come from web's
            // Checkout flow; PaymentIntent events come from mobile's PaymentSheet
            // flow. Both end up driving the same Order state transitions.
            return stripeEvent.Type switch
            {
                Constants.StripeEventType.ExpiredSession
                    => await HandleExpiredSession(order, orderId, cancellationToken),
                Constants.StripeEventType.CompletedSession
                    or Constants.StripeEventType.PaymentIntentSucceeded
                    => await HandleCompletedSession(order, orderId, command.Language, cancellationToken),
                Constants.StripeEventType.PaymentIntentPaymentFailed
                    => HandlePaymentIntentFailed(order, orderId),
                Constants.StripeEventType.PaymentIntentCanceled
                    => await HandleExpiredSession(order, orderId, cancellationToken),
                _ => BusinessResult.Success(orderId),
            };
        }

        /// <summary>
        /// PaymentIntent failed (declined card, insufficient funds, 3DS rejected).
        /// Keep the order in Pending so the mobile client can retry with a
        /// different payment method. Don't move to Cancelled — that path is
        /// reserved for explicit user cancellation or session expiry.
        /// </summary>
        private BusinessResult<string> HandlePaymentIntentFailed(Order order, string orderId)
        {
            logger.LogWarning(
                "PaymentIntent failed for order {OrderId} (status remains {Status}); client may retry",
                orderId, order.PaymentStatus);
            return BusinessResult.Success(orderId);
        }

        private async Task<BusinessResult<string>> HandleCompletedSession(Order order, string orderId, string language, CancellationToken cancellationToken)
        {
            if (order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded)
            {
                logger.LogInformation("Order {OrderId} already in terminal state {Status}, skipping webhook processing", orderId, order.PaymentStatus);
                return BusinessResult.Success(orderId);
            }

            order.UpdatePaymentStatus(PaymentStatus.Paid);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

            await queueClient.SendAsync(QueueNames.GenerateReceipt,
                new GenerateReceiptMessage(orderId, language), cancellationToken);

            if (!string.IsNullOrEmpty(order.UserId))
            {
                await queueClient.SendAsync(
                    QueueNames.NotificationsDispatch,
                    new SendPushNotificationMessage(
                        UserId: order.UserId,
                        EventKey: NotificationEventCatalog.OrderConfirmed,
                        Args: new Dictionary<string, string>
                        {
                            ["orderId"] = order.Id,
                            ["orderNumber"] = order.DisplayOrderNumber,
                        },
                        TenantId: order.TenantId),
                    cancellationToken);
            }

            logger.LogInformation("Successfully processed payment webhook for order {OrderId}", orderId);
            return BusinessResult.Success(orderId);
        }

        private async Task<BusinessResult<string>> HandleExpiredSession(Order order, string orderId, CancellationToken cancellationToken)
        {
            // Idempotency check - don't process if already cancelled or paid
            if (order.PaymentStatus is PaymentStatus.Failed or PaymentStatus.Paid or PaymentStatus.Refunded)
            {
                logger.LogInformation("Order {OrderId} already has payment status {Status}, skipping expired session", orderId, order.PaymentStatus);
                return BusinessResult.Success(orderId);
            }

            order.UpdatePaymentStatus(PaymentStatus.Failed);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

            if (!string.IsNullOrEmpty(order.UserId))
            {
                await queueClient.SendAsync(
                    QueueNames.NotificationsDispatch,
                    new SendPushNotificationMessage(
                        UserId: order.UserId,
                        EventKey: NotificationEventCatalog.OrderCancelled,
                        Args: new Dictionary<string, string>
                        {
                            ["orderId"] = order.Id,
                            ["orderNumber"] = order.DisplayOrderNumber,
                        },
                        TenantId: order.TenantId),
                    cancellationToken);
            }

            logger.LogInformation("Cancelled order {OrderId} due to expired Stripe checkout session", orderId);
            return BusinessResult.Success(orderId);
        }

    }
}