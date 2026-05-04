using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Core.Clients.Abstractions.SendGrid;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
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
                return IsOrderEvent(stripeEvent.Type) || IsSubscriptionEvent(stripeEvent.Type);
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
            if (IsSubscriptionEvent(stripeEvent.Type))
            {
                return true;
            }

            string? orderId = null;
            if (stripeEvent.Type is Constants.StripeEventType.CompletedSession
                                 or Constants.StripeEventType.ExpiredSession)
            {
                var session = stripeEvent.Data.Object as Session;
                orderId = session?.Metadata?.GetValueOrDefault("OrderId");
            }
            else if (stripeEvent.Type is Constants.StripeEventType.PaymentIntentSucceeded
                                      or Constants.StripeEventType.PaymentIntentPaymentFailed
                                      or Constants.StripeEventType.PaymentIntentCanceled)
            {
                var intent = stripeEvent.Data.Object as PaymentIntent;
                orderId = intent?.Metadata?.GetValueOrDefault("OrderId");
            }
            else
            {
                return true; // Unhandled event type — no order check needed
            }

            return !string.IsNullOrWhiteSpace(orderId)
                && await _orderRepository.ExistsAsync(orderId, cancellationToken);
        }

        internal static bool IsOrderEvent(string eventType) =>
            eventType is Constants.StripeEventType.CompletedSession
                      or Constants.StripeEventType.ExpiredSession
                      or Constants.StripeEventType.PaymentIntentSucceeded
                      or Constants.StripeEventType.PaymentIntentPaymentFailed
                      or Constants.StripeEventType.PaymentIntentCanceled;

        internal static bool IsSubscriptionEvent(string eventType) =>
            eventType is Constants.StripeEventType.SubscriptionCreated
                      or Constants.StripeEventType.SubscriptionUpdated
                      or Constants.StripeEventType.SubscriptionDeleted
                      or Constants.StripeEventType.InvoicePaymentFailed;
    }

    public record Command(string JsonPayload, string SignatureHeader, string Language = Constants.Language.English) : ICommand<string>;

    public class Handler(
        IStripeConfig stripeConfig,
        IOrderRepository orderRepository,
        IUserMembershipRepository userMembershipRepository,
        IMembershipPlanRepository membershipPlanRepository,
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

            // Subscription lifecycle (Cleansia Plus) — handle separately, no
            // order to look up. The local UserMembership row is the target;
            // we resolve by Stripe subscription id.
            if (Validator.IsSubscriptionEvent(stripeEvent.Type))
            {
                return await HandleSubscriptionEvent(stripeEvent, cancellationToken);
            }

            // Extract OrderId from whichever event variant we're handling.
            string? orderId = null;
            if (stripeEvent.Type is Constants.StripeEventType.CompletedSession
                                 or Constants.StripeEventType.ExpiredSession)
            {
                var session = stripeEvent.Data.Object as Session;
                orderId = session?.Metadata?.GetValueOrDefault("OrderId");
            }
            else if (stripeEvent.Type is Constants.StripeEventType.PaymentIntentSucceeded
                                      or Constants.StripeEventType.PaymentIntentPaymentFailed
                                      or Constants.StripeEventType.PaymentIntentCanceled)
            {
                var intent = stripeEvent.Data.Object as PaymentIntent;
                orderId = intent?.Metadata?.GetValueOrDefault("OrderId");
            }
            else
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

            var order = await orderRepository.GetByIdAsync(orderId, cancellationToken);
            if (order == null)
            {
                logger.LogError("Order {OrderId} not found", orderId);
                return BusinessResult.Failure<string>(new Error(
                    nameof(orderId),
                    BusinessErrorMessage.OrderNotFound));
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

        /// <summary>
        /// Reflect a Stripe subscription event onto the local UserMembership row.
        /// Resolves by Stripe subscription id; warns + no-ops if no local row
        /// matches (typical for Dashboard-created subs we never tracked).
        /// </summary>
        private async Task<BusinessResult<string>> HandleSubscriptionEvent(
            Event stripeEvent,
            CancellationToken cancellationToken)
        {
            // invoice.payment_failed carries an Invoice (not a Subscription); we
            // pull the subscription id from its `Subscription` field. The other
            // three event types carry a Subscription directly.
            string? subscriptionId = null;
            string stripeStatus = string.Empty;
            DateTime currentPeriodStart = default;
            DateTime currentPeriodEnd = default;

            if (stripeEvent.Type == Constants.StripeEventType.InvoicePaymentFailed)
            {
                var invoice = stripeEvent.Data.Object as Invoice;
                // Stripe.net 50.x: subscription id moved into Parent.SubscriptionDetails.
                // Older invoices (one-off charges, our checkout-session flow) have
                // no parent subscription → null id and we no-op below.
                subscriptionId = invoice?.Parent?.SubscriptionDetails?.SubscriptionId;
                stripeStatus = "past_due";
                // No reliable period bounds on the invoice payload — fall back
                // to whatever the existing local row has. UpdateFromStripeWebhook
                // will only flip Status, leaving CurrentPeriod* unchanged if
                // we pass the existing values.
            }
            else
            {
                var subscription = stripeEvent.Data.Object as Subscription;
                subscriptionId = subscription?.Id;
                stripeStatus = subscription?.Status ?? "canceled";
                // Period bounds live on each SubscriptionItem in Stripe.net 50.x.
                // We have a single Plus item per subscription, so the first
                // item's bounds are the subscription's bounds.
                var firstItem = subscription?.Items?.Data?.FirstOrDefault();
                currentPeriodStart = firstItem?.CurrentPeriodStart ?? DateTime.UtcNow;
                currentPeriodEnd = firstItem?.CurrentPeriodEnd ?? DateTime.UtcNow;
            }

            if (string.IsNullOrEmpty(subscriptionId))
            {
                logger.LogWarning(
                    "Subscription webhook {EventType} arrived without a subscription id; ignoring",
                    stripeEvent.Type);
                return BusinessResult.Success(string.Empty);
            }

            var membership = await userMembershipRepository
                .GetByStripeSubscriptionIdAsync(subscriptionId, cancellationToken);
            if (membership == null)
            {
                // No local row yet — only the customer.subscription.created
                // webhook from the web Checkout flow gets here (mobile creates
                // the row inline before the webhook arrives). Provision the
                // row from the Stripe subscription metadata stamped by
                // CreateMembershipCheckoutSession.
                if (stripeEvent.Type != Constants.StripeEventType.SubscriptionCreated)
                {
                    logger.LogWarning(
                        "Subscription webhook {EventType} for sub {SubscriptionId} has no local UserMembership row; ignoring",
                        stripeEvent.Type, subscriptionId);
                    return BusinessResult.Success(subscriptionId);
                }

                var stripeSub = stripeEvent.Data.Object as Subscription;
                var userId = stripeSub?.Metadata?.GetValueOrDefault("UserId");
                var planCode = stripeSub?.Metadata?.GetValueOrDefault("MembershipPlanCode");
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(planCode))
                {
                    logger.LogWarning(
                        "subscription.created webhook for sub {SubscriptionId} missing UserId/MembershipPlanCode metadata; can't provision local row",
                        subscriptionId);
                    return BusinessResult.Success(subscriptionId);
                }

                var plan = await membershipPlanRepository.GetByCodeAsync(planCode, cancellationToken);
                if (plan == null)
                {
                    logger.LogWarning(
                        "subscription.created webhook references unknown plan code {PlanCode}; can't provision local row",
                        planCode);
                    return BusinessResult.Success(subscriptionId);
                }

                membership = UserMembership.Create(
                    userId: userId,
                    membershipPlanId: plan.Id,
                    stripeSubscriptionId: subscriptionId,
                    currentPeriodStart: currentPeriodStart,
                    currentPeriodEnd: currentPeriodEnd);
                userMembershipRepository.Add(membership);

                logger.LogInformation(
                    "Provisioned UserMembership {MembershipId} for user {UserId} from subscription.created webhook (sub {SubscriptionId})",
                    membership.Id, userId, subscriptionId);
            }

            // For invoice.payment_failed we don't have fresh period bounds —
            // pass the existing ones so the row's CurrentPeriod* stays as-is.
            var startToWrite = currentPeriodStart == default
                ? membership.CurrentPeriodStart
                : currentPeriodStart;
            var endToWrite = currentPeriodEnd == default
                ? membership.CurrentPeriodEnd
                : currentPeriodEnd;

            membership.UpdateFromStripeWebhook(stripeStatus, startToWrite, endToWrite);

            logger.LogInformation(
                "Synced membership {MembershipId} (sub {SubscriptionId}) from {EventType}: status now {Status}",
                membership.Id, subscriptionId, stripeEvent.Type, membership.Status);

            return BusinessResult.Success(subscriptionId);
        }

        private async Task<BusinessResult<string>> HandleCompletedSession(Order order, string orderId, string language, CancellationToken cancellationToken)
        {
            // Idempotency check - don't process if already paid
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                logger.LogInformation("Order {OrderId} already marked as paid, skipping webhook processing", orderId);
                return BusinessResult.Success(orderId);
            }

            // Update payment status
            order.UpdatePaymentStatus(PaymentStatus.Paid);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

            // Enqueue receipt generation as a background job
            await queueClient.SendAsync(QueueNames.GenerateReceipt,
                new GenerateReceiptMessage(orderId, language), cancellationToken);

            logger.LogInformation("Successfully processed payment webhook for order {OrderId}", orderId);
            return BusinessResult.Success(orderId);
        }

        private async Task<BusinessResult<string>> HandleExpiredSession(Order order, string orderId, CancellationToken cancellationToken)
        {
            // Idempotency check - don't process if already cancelled or paid
            if (order.PaymentStatus is PaymentStatus.Failed or PaymentStatus.Paid)
            {
                logger.LogInformation("Order {OrderId} already has payment status {Status}, skipping expired session", orderId, order.PaymentStatus);
                return BusinessResult.Success(orderId);
            }

            order.UpdatePaymentStatus(PaymentStatus.Failed);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

            logger.LogInformation("Cancelled order {OrderId} due to expired Stripe checkout session", orderId);
            return BusinessResult.Success(orderId);
        }

    }
}