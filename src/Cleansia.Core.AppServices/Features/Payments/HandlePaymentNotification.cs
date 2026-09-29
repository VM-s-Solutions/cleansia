using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Core.AppServices.Services;
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

            // Tenant-ignoring existence check: the webhook is anonymous (no tenant claim), so the
            // tenant-scoped ExistsAsync collapses to TenantId == null and would reject any non-null-tenant
            // order — while the handler resolves it via GetByIdIgnoringTenantAsync. Mirror that read here.
            return !string.IsNullOrWhiteSpace(orderId)
                && await _orderRepository.ExistsIgnoringTenantAsync(orderId, cancellationToken);
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

    public record Command(string JsonPayload, string SignatureHeader, string Language = Constants.Language.English) : ICommand;

    /// <summary>Webhooks are anonymous — no JWT. Matches the sweeps and LoyaltyService.</summary>
    private const string SystemActor = "system";

    public class Handler(
        IStripeConfig stripeConfig,
        IOrderRepository orderRepository,
        ICreditAccountRepository creditAccountRepository,
        IDisputeRepository disputeRepository,
        IProcessedStripeEventRepository processedStripeEventRepository,
        IStripeSubscriptionWebhookHandler subscriptionWebhookHandler,
        ITenantProvider tenantProvider,
        IPendingDispatch pending,
        GuestOrderAccessTokenIssuer guestAccessTokenIssuer,
        INotificationProducer notificationProducer,
        IPreferredCleanerHoldResolver preferredCleanerHoldResolver,
        IAdminNotifier adminNotifier,
        IUserNotificationRepository userNotificationRepository,
        IStripeClientFactory stripeClientFactory,
        ITenantRepository tenantRepository,
        ISavedCardRepository savedCardRepository,
        IReceivableRepository receivableRepository,
        ILogger<Handler> logger) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
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
                return BusinessResult.Failure(new Error(
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
                return BusinessResult.Success();
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
                return BusinessResult.Success();
            }

            // A saved card lands here from either channel. A web capture raises both events, and the second
            // finds the card already captured.
            switch (stripeEvent.Data.Object)
            {
                case SetupIntent intent when stripeEvent.Type == Constants.StripeEventType.SetupIntentSucceeded:
                    return await CaptureSavedCard(
                        intent.Metadata?.GetValueOrDefault(SavedCardMetadataKey), intent.Id, cancellationToken);
                case Session { Mode: "setup" } session when stripeEvent.Type == Constants.StripeEventType.CompletedSession:
                    return await CaptureSavedCard(
                        session.Metadata?.GetValueOrDefault(SavedCardMetadataKey), session.SetupIntentId, cancellationToken);
            }

            // What a customer owes on an order is paid through its pay link or an off-session charge, and
            // either payment is the receivable's alone: the order's sale, charge surface and refunds are
            // not touched, so the order path below never sees these events.
            switch (stripeEvent.Data.Object)
            {
                case Session { Mode: "payment" } session when stripeEvent.Type == Constants.StripeEventType.CompletedSession
                    && session.Metadata?.GetValueOrDefault(ReceivableMetadataKey) is { Length: > 0 } paidByLink:
                    return await SettleReceivable(paidByLink, session.PaymentIntentId, command.Language, cancellationToken);
                case PaymentIntent intent when stripeEvent.Type == Constants.StripeEventType.PaymentIntentSucceeded
                    && intent.Metadata?.GetValueOrDefault(ReceivableMetadataKey) is { Length: > 0 } paidOffSession:
                    return await SettleReceivable(paidOffSession, intent.Id, command.Language, cancellationToken);
                case PaymentIntent intent when stripeEvent.Type == Constants.StripeEventType.PaymentIntentPaymentFailed
                    && intent.Metadata?.GetValueOrDefault(ReceivableMetadataKey) is { Length: > 0 } declined:
                    return await OfferPayLink(declined, intent, cancellationToken);
            }

            // Bank chargeback (ADR-0006 D4). No OrderId metadata — the event
            // resolves to the Order by its stored payment_intent, else through the
            // Checkout Session that charged it. Branched here, after the
            // idempotency gate (S7) and before the OrderId-metadata path below
            // which cannot resolve these.
            if (Constants.StripeEventType.IsChargebackEvent(stripeEvent.Type))
            {
                return await HandleChargeback(stripeEvent, cancellationToken);
            }

            var orderId = ExtractOrderId(stripeEvent);
            if (orderId is null)
            {
                logger.LogInformation("Received webhook event type {EventType}, ignoring", stripeEvent.Type);
                return BusinessResult.Success();
            }
            if (string.IsNullOrEmpty(orderId))
            {
                logger.LogError("Order ID not found in webhook metadata for event type {EventType}", stripeEvent.Type);
                return BusinessResult.Failure(new Error(
                    "OrderIdMissing",
                    "Order ID not found in webhook metadata"));
            }

            var order = await orderRepository.GetByIdIgnoringTenantAsync(orderId, cancellationToken);
            if (order == null)
            {
                logger.LogError("Order {OrderId} not found", orderId);
                return BusinessResult.Failure(new Error(
                    nameof(orderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            if (!string.IsNullOrEmpty(order.TenantId))
            {
                tenantProvider.SetTenantOverride(order.TenantId);
            }

            // A chargeback names only the PaymentIntent, and a web order is paid through a Checkout
            // Session, so the intent that moved the money is recorded here or the bank's claim finds no order.
            if (stripeEvent.Type == Constants.StripeEventType.CompletedSession
                && stripeEvent.Data.Object is Session { PaymentIntentId: { Length: > 0 } sessionPaymentIntentId })
            {
                order.AssignStripePaymentIntentId(sessionPaymentIntentId);
            }

            // Dispatch by event type. Checkout Session events come from web's
            // Checkout flow; PaymentIntent events come from mobile's PaymentSheet
            // flow. Both end up driving the same Order state transitions.
            return stripeEvent.Type switch
            {
                Constants.StripeEventType.ExpiredSession when order.RecurringTemplateId is not null
                    => ReleaseRecurringCheckout(order, (stripeEvent.Data.Object as Session)?.Id),
                Constants.StripeEventType.ExpiredSession
                    => await HandleExpiredSession(order, orderId, cancellationToken),
                Constants.StripeEventType.CompletedSession
                    or Constants.StripeEventType.PaymentIntentSucceeded
                    => await HandleCompletedSession(order, orderId, command.Language, cancellationToken),
                Constants.StripeEventType.PaymentIntentPaymentFailed
                    => await HandlePaymentIntentFailed(order, orderId, cancellationToken),
                Constants.StripeEventType.PaymentIntentCanceled
                    => await HandleExpiredSession(order, orderId, cancellationToken),
                _ => BusinessResult.Success(),
            };
        }

        /// <summary>
        /// PaymentIntent failed (declined card, insufficient funds, 3DS rejected).
        /// Keep the order in Pending so the mobile client can retry with a
        /// different payment method. Don't move to Cancelled — that path is
        /// reserved for explicit user cancellation or session expiry.
        /// </summary>
        private async Task<BusinessResult> HandlePaymentIntentFailed(Order order, string orderId, CancellationToken cancellationToken)
        {
            logger.LogWarning(
                "PaymentIntent failed for order {OrderId} (status remains {Status}); client may retry",
                orderId, order.PaymentStatus);

            // Stripe fires this per ATTEMPT and the platform resolves the state itself — a retry or the
            // stale sweep's cancel within the hour — so the administrators hear of the first decline on
            // an order and not of every fumbled card entry. Stripe also does not order a failed
            // attempt's event against the later success on the same PaymentIntent, so a decline that
            // lands after the money did, or after the order was cancelled, is news about nothing. The
            // subject is the order, and a repeated subject fails the commit on the outbox index rather
            // than collapsing, so the feed is read before the call: a row for this order means the
            // company was already told.
            if (!string.IsNullOrEmpty(order.TenantId)
                && order.PaymentStatus == PaymentStatus.Pending
                && order.CurrentStatus != OrderStatus.Cancelled
                && !await userNotificationRepository.AnyForEventAsync(
                    order.TenantId, AdminNotificationEventCatalog.PaymentFailed, "orderId", order.Id, cancellationToken))
            {
                await adminNotifier.NotifyAsync(
                    new AdminEvent(
                        AdminNotificationEventCatalog.PaymentFailed,
                        order.TenantId,
                        Subject: order.Id,
                        Args: new Dictionary<string, string>
                        {
                            ["orderNumber"] = order.DisplayOrderNumber,
                            ["orderId"] = order.Id,
                        }),
                    cancellationToken);
            }

            return BusinessResult.Success();
        }

        private async Task<BusinessResult> HandleCompletedSession(Order order, string orderId, string language, CancellationToken cancellationToken)
        {
            // Checked BEFORE the terminal-state short-circuit below: a cash-collected order is already
            // Paid, so it would otherwise be waved through as a benign duplicate — hiding the fact that
            // the customer just paid a second time.
            if (order.SettledInCash)
            {
                logger.LogError(
                    "Stripe settled order {OrderId} at {PaymentStatus} after employee {EmployeeId} collected it in cash on {CashCollectedAt}; escalating for manual reconciliation, no automatic refund",
                    orderId, order.PaymentStatus, order.CollectedByEmployeeId, order.CashCollectedAt);
                return await EscalateForReconciliation(order, orderId, DoubleSettlementDescription, cancellationToken);
            }

            if (order.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Refunded)
            {
                logger.LogInformation("Order {OrderId} already in terminal state {Status}, skipping webhook processing", orderId, order.PaymentStatus);
                return BusinessResult.Success();
            }

            // A checkout still open when its order was cancelled — by the customer, the stale-order sweep
            // or the recurring cutoff — can be paid afterwards. Recording Paid would issue a receipt and a
            // "payment confirmed" push for a clean nobody will do, so the money is left where it is and an
            // administrator refunds it.
            if (order.CurrentStatus == OrderStatus.Cancelled && order.TookNoPayment)
            {
                logger.LogError(
                    "Stripe settled order {OrderId} after it was cancelled ({CancellationReason}); escalating for a refund, the order is not marked paid",
                    orderId, order.CancellationReason);
                return await EscalateForReconciliation(order, orderId, PaidAfterCancellationDescription, cancellationToken);
            }

            // The MONEY axis only. This used to append OrderStatus.Confirmed too, which is what made
            // that word mean two unrelated things — "money settled" here, and "a cleaner took the job"
            // in TakeOrder. Owner ruling 2026-09-08 (T-0691): Confirmed means only the second, so a
            // paid card order rests at New + Paid, exactly as a cash order rests at New + Pending.
            //
            // Nothing is appended in its place. The fulfilment axis has not moved — no cleaner has
            // done anything — and a same-value re-append would put a second New track on the history
            // for an event that is not a fulfilment event at all.
            //
            // What still makes the order offerable is OrderAvailability: its status term admits New,
            // and its money term is satisfied by the line above. That relaxation shipped first,
            // deliberately, so the board was ready before any order could rest here.
            order.UpdatePaymentStatus(PaymentStatus.Paid);

            // ADR-0002 D1/D5 (the F2 webhook stamp/effect-split fix): record intent. These now fire
            // only AFTER the ProcessedStripeEvent stamp + state change commit; on a commit-throw
            // (incl. the parallel-retry 23505) the guard is unreached and nothing is dispatched — so a
            // Stripe retry can no longer produce a SECOND receipt + push.
            pending.Enqueue(
                QueueNames.GenerateReceipt,
                new QueueEnvelope<GenerateReceiptMessage>(
                    MessageKeys.Receipt(orderId),
                    order.TenantId,
                    new GenerateReceiptMessage(orderId, language)),
                MessageKeys.Receipt(orderId));

            if (!string.IsNullOrEmpty(order.UserId))
            {
                await notificationProducer.NotifyAsync(
                    order.UserId,
                    NotificationEventCatalog.OrderPaymentConfirmed,
                    new Dictionary<string, string>
                    {
                        ["orderId"] = order.Id,
                        ["orderNumber"] = order.DisplayOrderNumber,
                    },
                    order.TenantId,
                    order.Id,
                    cancellationToken);
            }

            // Q-BROWSE-01 (b): the payment write above is what makes a card order offerable, so this is
            // where its preferred cleaner is told. Creation could not: until the money lands the order
            // is New + Pending, the browse gate refuses it, and CleanupStalePendingOrders cancels it an
            // hour later. Runs after the terminal-state short-circuit, so a Stripe redelivery cannot
            // produce a second announcement.
            await PreferredOfferNotifier.NotifyBecameOfferableAsync(
                order, preferredCleanerHoldResolver, notificationProducer, DateTime.UtcNow, cancellationToken);
            await NewOrderAdminNotifier.NotifyIfOfferableAsync(order, adminNotifier, logger, cancellationToken);

            logger.LogInformation("Successfully processed payment webhook for order {OrderId}", orderId);
            return BusinessResult.Success();
        }

        private async Task<BusinessResult> HandleExpiredSession(Order order, string orderId, CancellationToken cancellationToken)
        {
            // Idempotency check - don't process if already cancelled or paid. The stale-order sweep may
            // have cancelled it first, and a second Cancelled track and notice would say it twice.
            if (order.PaymentStatus is PaymentStatus.Failed or PaymentStatus.Paid or PaymentStatus.Refunded
                || order.CurrentStatus == OrderStatus.Cancelled)
            {
                logger.LogInformation("Order {OrderId} already has payment status {Status}, skipping expired session", orderId, order.PaymentStatus);
                return BusinessResult.Success();
            }

            // The same cancellation the stale-order sweep writes: who, when and why, fee- and refund-free.
            order.UpdatePaymentStatus(PaymentStatus.Failed);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
            order.Cancel(
                DateTime.UtcNow,
                CancelledBy.System,
                feeRate: 0m,
                refundAmount: 0m,
                reason: OrderCancellationReasons.PaymentNotCompleted);

            // The session expired without a charge, but CreateOrder debits credit BEFORE the customer
            // ever reaches Stripe - so an abandoned checkout is the single most common way a customer
            // would lose credit for nothing. Keyed on the order id, so a re-delivered webhook and the
            // stale-order sweep that may also reach this order return it exactly once.
            await creditAccountRepository.ReturnUnpaidOrderCreditAsync(
                order, SystemActor, cancellationToken);

            if (!string.IsNullOrEmpty(order.UserId))
            {
                await notificationProducer.NotifyAsync(
                    order.UserId,
                    NotificationEventCatalog.OrderCancelled,
                    new Dictionary<string, string>
                    {
                        ["orderId"] = order.Id,
                        ["orderNumber"] = order.DisplayOrderNumber,
                    },
                    order.TenantId,
                    order.Id,
                    cancellationToken);
            }

            await GuestCancellationEmail.EnqueueAsync(order, languageCode: null,
                successfulRefundAmount: null, guestAccessTokenIssuer, pending, cancellationToken);

            logger.LogInformation("Cancelled order {OrderId} due to expired Stripe checkout session", orderId);
            return BusinessResult.Success();
        }

        /// <summary>
        /// A recurring occurrence's web checkout closed unpaid. The checkout did not create the occurrence,
        /// so it does not cancel it either: the occurrence stays for the customer to confirm again, on
        /// either channel, until AutoCancelStaleRecurringOrders retracts it at its cutoff — as an abandoned
        /// PaymentSheet leaves it. Only the session it records is forgotten; a session it no longer records
        /// closing late changes nothing.
        /// </summary>
        private BusinessResult ReleaseRecurringCheckout(Order order, string? sessionId)
        {
            if (!string.IsNullOrEmpty(sessionId) && order.StripeSessionId == sessionId)
            {
                order.AssignStripeSessionId(string.Empty);
            }

            logger.LogInformation(
                "Checkout session {SessionId} for recurring order {OrderId} expired; the occurrence stays confirmable",
                sessionId, order.Id);
            return BusinessResult.Success();
        }

        private const string SavedCardMetadataKey = "SavedCardId";

        /// <summary>
        /// The card a customer saved lands on the row their capture started. A SetupIntent with no saved
        /// card behind it — the Plus subscribe flow's — is not this flow's and is ignored. The card is read
        /// from Stripe, so an unreachable Stripe throws, the processed-event stamp rolls back and Stripe
        /// retries. A new card replaces the customer's earlier one in the same currency.
        /// </summary>
        private async Task<BusinessResult> CaptureSavedCard(
            string? savedCardId, string? setupIntentId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(savedCardId) || string.IsNullOrEmpty(setupIntentId))
            {
                logger.LogInformation("Setup event {SetupIntentId} carries no saved card; ignoring", setupIntentId);
                return BusinessResult.Success();
            }

            var card = await savedCardRepository.GetByIdIgnoringTenantAsync(savedCardId, cancellationToken);
            if (card is null || card.IsCaptured || !card.IsActive)
            {
                logger.LogInformation(
                    "Saved card {SavedCardId} is unknown, already captured or removed; setup event {SetupIntentId} ignored",
                    savedCardId, setupIntentId);
                return BusinessResult.Success();
            }

            if (!string.IsNullOrEmpty(card.TenantId))
            {
                tenantProvider.SetTenantOverride(card.TenantId);
            }

            var details = await stripeClientFactory.CreateClient().GetSetupIntentCardAsync(setupIntentId, cancellationToken);
            if (details is null)
            {
                logger.LogWarning(
                    "Setup intent {SetupIntentId} for saved card {SavedCardId} saved no card; nothing captured",
                    setupIntentId, card.Id);
                return BusinessResult.Success();
            }

            foreach (var replaced in await savedCardRepository.GetCapturedForUserInCurrencyAsync(
                         card.UserId, card.CurrencyId, cancellationToken))
            {
                savedCardRepository.Deactivate(replaced);
            }

            card.Capture(details.PaymentMethodId, details.Brand, details.Last4, details.ExpMonth, details.ExpYear);
            logger.LogInformation("Captured saved card {SavedCardId} for user {UserId}", card.Id, card.UserId);
            return BusinessResult.Success();
        }

        private const string ReceivableMetadataKey = "ReceivableId";

        /// <summary>
        /// The receivable is paid, and its fee receipt is asked for. A second payment of one already paid is
        /// money taken twice for one debt, and it is refunded in full, keyed on its own PaymentIntent so a
        /// redelivery replays the same refund. The refund is made here, so an unreachable Stripe throws, the
        /// processed-event stamp rolls back and Stripe redelivers.
        /// </summary>
        private async Task<BusinessResult> SettleReceivable(
            string receivableId, string? paymentIntentId, string language, CancellationToken cancellationToken)
        {
            var receivable = await receivableRepository.GetByIdIgnoringTenantAsync(receivableId, cancellationToken);
            if (receivable is null)
            {
                logger.LogWarning("Payment {PaymentIntentId} names unknown receivable {ReceivableId}; ignoring", paymentIntentId, receivableId);
                return BusinessResult.Success();
            }

            if (!string.IsNullOrEmpty(receivable.TenantId))
            {
                tenantProvider.SetTenantOverride(receivable.TenantId);
            }

            if (receivable.IsPaid)
            {
                if (!string.IsNullOrEmpty(paymentIntentId) && receivable.StripePaymentIntentId != paymentIntentId)
                {
                    await stripeClientFactory.CreateClient().RefundPaymentIntentAsync(
                        paymentIntentId,
                        receivable.Amount,
                        $"refund:receivable:{receivable.Id}:{paymentIntentId}",
                        cancellationToken);
                    logger.LogWarning(
                        "Receivable {ReceivableId} paid by {PaymentIntentId} was paid again by {SecondPaymentIntentId}; the second payment was refunded in full",
                        receivable.Id, receivable.StripePaymentIntentId, paymentIntentId);
                }

                return BusinessResult.Success();
            }

            receivable.MarkPaid(paymentIntentId, DateTimeOffset.UtcNow);

            var key = MessageKeys.FeeReceipt(receivable.Id);
            pending.Enqueue(
                QueueNames.GenerateReceipt,
                new QueueEnvelope<GenerateReceiptMessage>(
                    key, receivable.TenantId, new GenerateReceiptMessage(receivable.OrderId, language, receivable.Id)),
                key);

            logger.LogInformation("Receivable {ReceivableId} paid by {PaymentIntentId}", receivable.Id, paymentIntentId);
            return BusinessResult.Success();
        }

        /// <summary>
        /// An off-session charge was declined, or the bank asked for the customer to authenticate: the
        /// customer is e-mailed a pay link for the amount (owner ruling 2026-09-28, decision 18). The link is
        /// created here, so an unreachable Stripe throws, the processed-event stamp rolls back and Stripe
        /// redelivers. With card payments switched off no link is made and the receivable stays open.
        /// </summary>
        private async Task<BusinessResult> OfferPayLink(string receivableId, PaymentIntent intent, CancellationToken cancellationToken)
        {
            var receivable = await receivableRepository.GetByIdIgnoringTenantAsync(receivableId, cancellationToken);
            if (receivable is not { IsOpen: true })
            {
                logger.LogInformation(
                    "Failed charge {PaymentIntentId} names receivable {ReceivableId}, which is not open; no pay link",
                    intent.Id, receivableId);
                return BusinessResult.Success();
            }

            if (!string.IsNullOrEmpty(receivable.TenantId))
            {
                tenantProvider.SetTenantOverride(receivable.TenantId);
            }

            logger.LogWarning(
                "Off-session charge {PaymentIntentId} for receivable {ReceivableId} failed ({FailureCode})",
                intent.Id, receivable.Id, intent.LastPaymentError?.Code);

            if (!stripeConfig.Enabled)
            {
                logger.LogWarning("Card payments are switched off; receivable {ReceivableId} stays open with no pay link", receivable.Id);
                return BusinessResult.Success();
            }

            var link = await stripeClientFactory.CreateClient().CreateReceivableCheckoutSessionAsync(
                receivable.Id,
                receivable.PayLinkSessionId,
                receivable.OrderId,
                receivable.Order!.DisplayOrderNumber,
                receivable.Amount,
                receivable.Currency!.Code,
                cancellationToken);
            receivable.RecordPayLink(link.Id);

            var key = MessageKeys.ReceivablePayLinkEmail(receivable.Id, receivable.Attempts);
            pending.Enqueue(
                QueueNames.SendEmail,
                new QueueEnvelope<SendReceivablePayLinkEmailMessage>(
                    key,
                    receivable.TenantId,
                    new SendReceivablePayLinkEmailMessage(receivable.Id, receivable.Attempts, link.Url, receivable.TenantId)),
                key);

            return BusinessResult.Success();
        }

        private const string WebhookActor = "stripe-webhook";

        private const string DoubleSettlementDescription =
            "The card payment settled at Stripe after the cleaner had already collected this order in cash. " +
            "The customer may have paid twice — reconcile the two settlements and decide the refund.";

        private const string PaidAfterCancellationDescription =
            "The card payment settled at Stripe after this order was cancelled, so the customer paid for a " +
            "clean that will not take place. Refund the payment.";

        /// <summary>
        /// A card charge settled that the order should not have taken: after the assigned cleaner
        /// recorded a cash collection, or after the order was cancelled. Deliberately does not refund:
        /// which settlement to reverse, and whether a cancellation fee stands, is a human call, so this
        /// raises an escalated dispute for an administrator and leaves the money exactly where it is.
        /// </summary>
        private async Task<BusinessResult> EscalateForReconciliation(
            Order order, string orderId, string description, CancellationToken cancellationToken)
        {
            var existing = await disputeRepository.GetOpenDisputeForOrderAsync(order.Id, cancellationToken);
            if (existing is not null)
            {
                if (!existing.UpdateStatus(DisputeStatus.Escalated, WebhookActor))
                {
                    logger.LogWarning(
                        "Late settlement on order {OrderId} could not escalate the open dispute (illegal {CurrentStatus} → Escalated)",
                        orderId, existing.Status);
                }
                return BusinessResult.Success();
            }

            var dispute = new Cleansia.Core.Domain.Disputes.Dispute(
                orderId: order.Id,
                userId: order.UserId,
                reason: DisputeReason.IncorrectAmount,
                description: description,
                createdBy: WebhookActor);

            if (!dispute.UpdateStatus(DisputeStatus.Escalated, WebhookActor))
            {
                logger.LogWarning(
                    "Late settlement on order {OrderId} could not escalate a new dispute (illegal {CurrentStatus} → Escalated); not persisting",
                    orderId, dispute.Status);
                return BusinessResult.Success();
            }
            disputeRepository.Add(dispute);

            return BusinessResult.Success();
        }

        /// <summary>
        /// Inbound bank chargeback (ADR-0006 D4). Resolves the disputed charge to
        /// our Order by its stored payment_intent, else through the Checkout Session
        /// that charged it, then links a Dispute (created if absent) to the Stripe
        /// dispute id and reflects Stripe's status. A Stripe outage during the
        /// session lookup throws, so the processed-event stamp rolls back and Stripe
        /// retries. A charge.dispute.created that still matches no Order alerts the
        /// administrators of every company.
        /// </summary>
        private async Task<BusinessResult> HandleChargeback(Event stripeEvent, CancellationToken cancellationToken)
        {
            var stripeDispute = stripeEvent.Data.Object as Stripe.Dispute;
            var paymentIntentId = stripeDispute?.PaymentIntentId;
            var stripeDisputeId = stripeDispute?.Id;

            if (string.IsNullOrWhiteSpace(paymentIntentId) || string.IsNullOrWhiteSpace(stripeDisputeId))
            {
                logger.LogWarning("Chargeback event {EventType} missing payment_intent or dispute id; ignoring", stripeEvent.Type);
                return BusinessResult.Success();
            }

            if (stripeEvent.Type is Constants.StripeEventType.ChargeDisputeUpdated
                                 or Constants.StripeEventType.ChargeDisputeClosed)
            {
                return await ReflectChargebackStatus(stripeDispute!, stripeDisputeId, stripeEvent, cancellationToken);
            }

            var order = await orderRepository.GetByStripePaymentIntentIdIgnoringTenantAsync(paymentIntentId, cancellationToken)
                ?? await FindByCheckoutSessionAsync(paymentIntentId, cancellationToken);
            if (order is null)
            {
                logger.LogWarning("Chargeback {EventType} resolved to no local order; telling every company", stripeEvent.Type);
                await TellEveryCompanyOfUnmatchedChargeback(stripeDispute!, cancellationToken);
                return BusinessResult.Success();
            }

            if (!string.IsNullOrEmpty(order.TenantId))
            {
                tenantProvider.SetTenantOverride(order.TenantId);
            }

            var existing = await disputeRepository.GetOpenDisputeForOrderAsync(order.Id, cancellationToken);
            if (existing is not null)
            {
                existing.LinkStripeDispute(stripeDisputeId, WebhookActor);
                await TellAdministratorsOfChargeback(order, existing.Id, stripeDispute!, cancellationToken);
                logger.LogInformation("Linked chargeback to existing dispute for order {OrderId}", order.Id);
                return BusinessResult.Success();
            }

            var dispute = new Cleansia.Core.Domain.Disputes.Dispute(
                orderId: order.Id,
                userId: order.UserId,
                reason: DisputeReason.Chargeback, // ADR-0006 D4: a bank chargeback, not a customer claim
                description: ChargebackDescription,
                createdBy: WebhookActor);
            dispute.LinkStripeDispute(stripeDisputeId, WebhookActor);

            // Route the terminal write through the same transition guard the in-app path obeys
            // (CanTransitionTo) rather than forcing the edge via a bare Escalate. A freshly-built
            // dispute is Pending and Pending→Escalated is legal, so the happy path is unchanged; a
            // non-legal start state is rejected here instead of being silently overwritten.
            if (!dispute.UpdateStatus(DisputeStatus.Escalated, WebhookActor))
            {
                logger.LogWarning(
                    "Chargeback {EventType} could not escalate a new dispute for order {OrderId} " +
                    "(illegal {CurrentStatus} → Escalated); not persisting",
                    stripeEvent.Type, order.Id, dispute.Status);
                return BusinessResult.Success();
            }
            disputeRepository.Add(dispute);
            await TellAdministratorsOfChargeback(order, dispute.Id, stripeDispute!, cancellationToken);

            logger.LogInformation("Created and linked chargeback dispute for order {OrderId}", order.Id);
            return BusinessResult.Success();
        }

        private const string ChargebackDescription = "Bank chargeback raised against this order's payment.";

        // A web order paid before its intent was recorded carries only its Checkout Session.
        private async Task<Order?> FindByCheckoutSessionAsync(string paymentIntentId, CancellationToken cancellationToken)
        {
            var orderId = await stripeClientFactory.CreateClient()
                .FindCheckoutSessionOrderIdAsync(paymentIntentId, cancellationToken);
            if (string.IsNullOrEmpty(orderId))
            {
                return null;
            }

            var order = await orderRepository.GetByIdIgnoringTenantAsync(orderId, cancellationToken);
            order?.AssignStripePaymentIntentId(paymentIntentId);
            return order;
        }

        // The Stripe account is shared by every operating company, so a claim no order carries could be
        // any company's money, and each is told.
        private async Task TellEveryCompanyOfUnmatchedChargeback(Stripe.Dispute stripeDispute, CancellationToken cancellationToken)
        {
            var amount = MoneyText.Format(stripeDispute.Amount / 100m, stripeDispute.Currency?.ToUpperInvariant() ?? string.Empty);
            foreach (var tenantId in await tenantRepository.GetAllIdsAsync(cancellationToken))
            {
                await adminNotifier.NotifyAsync(
                    new AdminEvent(
                        AdminNotificationEventCatalog.DisputeChargebackUnmatched,
                        tenantId,
                        Subject: $"{stripeDispute.Id}:{tenantId}",
                        Args: new Dictionary<string, string>
                        {
                            ["amount"] = amount,
                            ["stripeDisputeId"] = stripeDispute.Id,
                        }),
                    cancellationToken);
            }
        }

        /// <summary>
        /// The dispute named is the one the money is now attached to — the customer's open one when
        /// there is one, else the chargeback's own — so the console opens the right file. The amount is
        /// what the bank pulled, in Stripe's minor units, shown in the order's currency.
        /// </summary>
        private Task TellAdministratorsOfChargeback(Order order, string disputeId, Stripe.Dispute stripeDispute, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(order.TenantId))
            {
                return Task.CompletedTask;
            }

            return adminNotifier.NotifyAsync(
                new AdminEvent(
                    AdminNotificationEventCatalog.DisputeChargeback,
                    order.TenantId,
                    Subject: stripeDispute.Id,
                    Args: new Dictionary<string, string>
                    {
                        ["orderNumber"] = order.DisplayOrderNumber,
                        ["amount"] = MoneyText.Format(stripeDispute.Amount / 100m, order.Currency!),
                        ["disputeId"] = disputeId,
                        ["orderId"] = order.Id,
                    }),
                cancellationToken);
        }

        private async Task<BusinessResult> ReflectChargebackStatus(
            Stripe.Dispute stripeDispute, string stripeDisputeId, Event stripeEvent, CancellationToken cancellationToken)
        {
            // Tenant-ignoring read: the webhook is anonymous (no tenant claim), so a tenant-scoped read
            // would collapse to TenantId == null and miss any non-null-tenant dispute (ADR-0006 D4).
            var dispute = await disputeRepository.GetByStripeDisputeIdIgnoringTenantAsync(stripeDisputeId, cancellationToken);
            if (dispute is null)
            {
                logger.LogWarning("Chargeback {EventType} for unknown linked dispute; ignoring", stripeEvent.Type);
                return BusinessResult.Success();
            }

            // Re-scope BEFORE the status write so the Updated(...) audit commit lands under the
            // dispute's tenant (the read bypassed the filter; the write must not).
            if (!string.IsNullOrEmpty(dispute.TenantId))
            {
                tenantProvider.SetTenantOverride(dispute.TenantId);
            }

            // The webhook is a SECOND writer on the same legal graph as the in-app UpdateStatus path,
            // so it obeys the same transition guard (ADR-0006 D4 / the T-0172 table) rather than forcing
            // an edge. Map Stripe status → target first, then gate before any write.
            var target = MapStripeStatusToDisputeStatus(stripeDispute.Status);

            // Idempotent redelivery: target already reached (e.g. a second charge.dispute.updated still
            // mapping to Escalated). Self-edges are absent from the table by design — benign no-op.
            if (target == dispute.Status)
            {
                return BusinessResult.Success();
            }

            switch (target)
            {
                // Resolve is owned OUTSIDE the CanTransitionTo table (Dispute.cs comment), so the "won"
                // arm gates on terminality instead: never resolve an already-settled dispute (this is the
                // Closed→Resolved late-event hazard).
                case DisputeStatus.Resolved when !dispute.IsTerminal:
                    dispute.Resolve(WebhookActor, refundAmount: null, resolutionNotes: ChargebackWonNotes);
                    break;
                case DisputeStatus.Closed when dispute.CanTransitionTo(DisputeStatus.Closed):
                    dispute.Close(WebhookActor);
                    break;
                case DisputeStatus.Escalated when dispute.CanTransitionTo(DisputeStatus.Escalated):
                    dispute.Escalate(WebhookActor);
                    break;
                default:
                    // A genuine forbidden edge (e.g. "won" after a prior "lost" left it Closed). No-op +
                    // warn; never a retry-inducing failure for a benign late event (S6).
                    logger.LogWarning(
                        "Chargeback {EventType} would force an illegal transition on dispute {DisputeId} " +
                        "({CurrentStatus} → {Target}); ignoring",
                        stripeEvent.Type, dispute.Id, dispute.Status, target);
                    return BusinessResult.Success();
            }

            logger.LogInformation("Reflected chargeback {EventType} status onto dispute for order {OrderId}", stripeEvent.Type, dispute.OrderId);
            return BusinessResult.Success();
        }

        /// <summary>
        /// Maps a Stripe dispute status to the target <see cref="DisputeStatus"/> (ADR-0006 D4):
        /// <c>won</c> → Resolved (settled in our favor), <c>lost</c> → Closed (the bank pulled funds),
        /// everything else (<c>needs_response</c>, <c>under_review</c>, <c>warning_*</c>, …) → Escalated.
        /// </summary>
        private static DisputeStatus MapStripeStatusToDisputeStatus(string stripeStatus) => stripeStatus switch
        {
            "won" => DisputeStatus.Resolved,
            "lost" => DisputeStatus.Closed,
            _ => DisputeStatus.Escalated,
        };

        private const string ChargebackWonNotes = "Chargeback won; funds retained.";
    }
}