using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Customer confirmation of an occurrence the recurring materializer spawned. Both tenders stamp
/// <see cref="Order.CustomerConfirmedAt"/>. Card then asks for the money — a PaymentIntent for the
/// mobile PaymentSheet, a Checkout Session on the web — and the order is paid only on the webhook. Cash
/// is confirmed by the stamp alone and stays <see cref="PaymentStatus.Pending"/> until the cleaner
/// records the cash, so its receipt is issued at completion and the customer gets the booking e-mail.
///
/// <para>Refuses orders that are not awaiting confirmation, not owned by the caller, or not linked to a
/// template — those belong on the standard booking flow — a cash occurrence whose job needs more
/// than one cleaner or that <see cref="CustomerCashStanding"/> does not admit, and an occurrence closer
/// than the minimum lead time a one-off booking gets.
/// → /flows/booking-and-pricing#recurring-bookings</para>
/// </summary>
[AuditAction("customer.order.recurring.confirm", Audience = AuditAudience.Customer, ResourceType = "Order")]
public class ConfirmRecurringOrder
{
    public record Command(string OrderId) : ICommand<Response>;

    /// <summary>
    /// The occurrence the customer confirmed, priced as the materializer stored it (ADR-0062 D3). On the
    /// card flavour the row records the confirmation the customer initiated; the money moves on the webhook.
    /// </summary>
    public record RecurringOccurrenceConfirmationEvidence(
        string OrderId,
        string RecurringTemplateId,
        decimal TotalPrice,
        string? CurrencyCode,
        PaymentType PaymentType,
        DirtinessLevel DirtinessLevel,
        DateTimeOffset CleaningDateTime,
        decimal LeadTimeHours) : ICustomerAuditPayload;

    /// <summary>
    /// Both flavors return the same shape. A mobile card confirm carries <see cref="ClientSecret"/> for
    /// the PaymentSheet, a web card confirm carries <see cref="CheckoutUrl"/> to redirect to, and a cash
    /// confirm carries neither.
    /// </summary>
    public record Response(
        string OrderId,
        string? ClientSecret,
        string? PaymentIntentId,
        string? StripeCustomerId,
        string? EphemeralKey,
        string? CheckoutUrl = null);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IOrderAccessService orderAccessService,
        IOrderRepository orderRepository,
        ISavedCardRepository savedCardRepository,
        IReceivableRepository receivableRepository,
        ICreditAccountRepository creditAccountRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        ITenantProvider tenantProvider,
        IStripeClient stripeClient,
        IStripeConfig stripeConfig,
        IOrderChannelProvider channelProvider,
        IPendingDispatch pending,
        INotificationProducer notificationProducer,
        IPreferredCleanerHoldResolver preferredCleanerHoldResolver,
        IAdminNotifier adminNotifier,
        IAuditContext auditContext,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        private static readonly TimeSpan CheckoutStride = TimeSpan.FromHours(23);

        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var sessionUserId = userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(sessionUserId))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var order = await orderAccessService.LoadOrderForCallerAsync(command.OrderId, cancellationToken);
            if (order == null
                || order.UserId != sessionUserId
                || string.IsNullOrEmpty(order.RecurringTemplateId))
            {
                // Hide all three failure modes behind the same NotFound error so
                // the response doesn't leak whether an order id exists.
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            if (order.CurrentStatus == OrderStatus.Cancelled)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.OrderAlreadyCancelled));
            }

            if (order.PaymentStatus != PaymentStatus.Pending)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentStatus), BusinessErrorMessage.OrderPaymentAlreadyPaid));
            }

            if (!order.AwaitsCustomerConfirmation)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.OrderRecurringAlreadyConfirmed));
            }

            if (BookingPolicy.IsBelowMinimumLeadTime(order.CleaningDateTime, DateTime.UtcNow))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.CleaningDateTime), BusinessErrorMessage.CleaningDateBelowLeadTime));
            }

            // An occurrence materialized before the one-cleaner cash rule. It is neither confirmed as cash
            // nor switched to card: the customer cancels it (free while nobody has taken it) and moves the
            // template to card. → /flows/booking-and-pricing#recurring-bookings
            if (order.PaymentType == PaymentType.Cash
                && !BookingPolicy.AllowsCash(!string.IsNullOrEmpty(order.UserId), order.RequiredEmployees))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentType), BusinessErrorMessage.OrderCashNotAvailable));
            }

            if (order.PaymentType == PaymentType.Cash
                && !await CustomerCashStanding.OwesNothingAsync(
                    receivableRepository, sessionUserId, cancellationToken))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentType), BusinessErrorMessage.OrderCashUnpaidReceivable));
            }

            if (order.PaymentType == PaymentType.Cash
                && !await CustomerCashStanding.HasRoomForAnotherOpenCashBookingAsync(
                    orderRepository, sessionUserId, cancellationToken))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentType), BusinessErrorMessage.OrderCashOpenBookingsLimitReached));
            }

            if (order.PaymentType == PaymentType.Cash
                && !await CustomerCashStanding.HoldsUsableCardAsync(
                    savedCardRepository, sessionUserId, order.CurrencyId, cancellationToken))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentType), BusinessErrorMessage.OrderCashRequiresSavedCard));
            }

            var result = order.PaymentType switch
            {
                PaymentType.Cash => await HandleCashAsync(order, cancellationToken),
                PaymentType.Card => await HandleCardAsync(order, sessionUserId, cancellationToken),
                _ => BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentType), BusinessErrorMessage.InvalidEnumValue)),
            };

            if (result.IsSuccess)
            {
                // Card confirmation reads the account first; its success audit follows the order.
                if (!string.IsNullOrEmpty(order.TenantId))
                {
                    tenantProvider.SetTenantOverride(order.TenantId);
                }

                var nowUtc = DateTime.UtcNow;
                auditContext.RecordEvidence("Order", order.Id, new RecurringOccurrenceConfirmationEvidence(
                    OrderId: order.Id,
                    RecurringTemplateId: order.RecurringTemplateId,
                    TotalPrice: order.TotalPrice,
                    CurrencyCode: order.Currency?.Code,
                    PaymentType: order.PaymentType,
                    DirtinessLevel: order.DirtinessLevel,
                    CleaningDateTime: new DateTimeOffset(DateTime.SpecifyKind(order.CleaningDateTime, DateTimeKind.Utc)),
                    LeadTimeHours: Math.Round((decimal)(order.CleaningDateTime - nowUtc).TotalHours, 2)));
            }

            return result;
        }

        private async Task<BusinessResult<Response>> HandleCashAsync(
            Order order, CancellationToken cancellationToken)
        {
            if (order.TenantId is not null) tenantProvider.SetTenantOverride(order.TenantId);

            // Neither axis moves: no money has changed hands, and the customer confirming their own
            // occurrence is not a cleaner taking it (owner ruling 2026-09-08). The stamp is what
            // OrderAvailability admits a recurring cash occurrence on; the cleaner records the cash at
            // the door and the receipt follows at completion (owner ruling 2026-09-28).
            order.ConfirmByCustomer(DateTime.UtcNow);

            OrderBookedEmail.Enqueue(order, Constants.Language.English, pending);

            // Q-BROWSE-01 (b): an unconfirmed recurring cash occurrence is not offerable, because
            // AutoCancelStaleRecurringOrders retracts it. The stamp above is that transition, so this is
            // where its preferred cleaner is told. The card flavour is told by the Stripe webhook
            // instead, and the confirmation guard upstream keeps either to one announcement.
            await PreferredOfferNotifier.NotifyBecameOfferableAsync(
                order, preferredCleanerHoldResolver, notificationProducer, DateTime.UtcNow, cancellationToken);
            await NewOrderAdminNotifier.NotifyIfOfferableAsync(order, adminNotifier, logger, cancellationToken);

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                ClientSecret: null,
                PaymentIntentId: null,
                StripeCustomerId: null,
                EphemeralKey: null));
        }

        /// <summary>
        /// Take whatever credit this occurrence may use, and answer with the amount actually taken.
        /// The same shape as <c>CreateOrder.TakeCreditForOrderAsync</c>, for the same reasons — card
        /// only, matching currency, capped so the card always pays a share, and debited BEFORE the
        /// amount is used so the database arbitrates two confirmations racing each other.
        ///
        /// <para>No compensating return here, unlike CreateOrder: this handler mints a PaymentIntent
        /// rather than a redirect, and if Stripe throws the order still exists in a confirmable state
        /// with its credit recorded. A retry re-enters on the same order id, sees a non-zero
        /// <c>CreditAppliedAmount</c> and takes nothing more.</para>
        /// </summary>
        private async Task<decimal> TakeCreditForOrderAsync(
            Order order, string userId, CancellationToken cancellationToken)
        {
            if (order.PaymentType != PaymentType.Card || string.IsNullOrEmpty(userId))
            {
                return 0m;
            }

            // Asked FOR the order's currency rather than asked-then-compared. The comparison below is
            // kept as a belt-and-braces assertion on a money path, but it can no longer be the thing
            // that decides: an unkeyed read returned whichever account existed, so a customer with a
            // matching balance and a second account could be told they had none.
            var spendable = await creditAccountRepository.GetSpendableAsync(
                userId, order.CurrencyId, cancellationToken);
            if (spendable == null || spendable.CurrencyId != order.CurrencyId)
            {
                return 0m;
            }

            var eligible = BookingPolicy.CapCreditForOrder(spendable.Balance, order.TotalPrice);
            if (eligible <= 0m)
            {
                return 0m;
            }

            var taken = await creditAccountRepository.TryDebitAsync(
                creditAccountId: spendable.AccountId,
                amount: eligible,
                reason: CreditTransactionReason.OrderPayment,
                idempotencyKey: $"order-payment-{order.Id}",
                actorId: userId,
                cancellationToken: cancellationToken,
                orderId: order.Id);

            if (!taken)
            {
                logger.LogInformation(
                    "Credit debit of {Amount} was refused for recurring order {OrderId}; the balance "
                    + "moved, so the occurrence is confirmed at full price.",
                    eligible, order.Id);
                return 0m;
            }

            return eligible;
        }

        private async Task<BusinessResult<Response>> HandleCardAsync(
            Order order, string sessionUserId, CancellationToken cancellationToken)
        {

            // Card payments can be switched off platform-wide. Checked before the Stripe customer is
            // created, so a refused payment leaves nothing behind. -> IStripeConfig
            if (!stripeConfig.Enabled)
            {
                logger.LogWarning(
                    "Recurring card confirm refused for order {OrderId}: card payments are disabled "
                    + "(Stripe:Enabled=false)", order.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.Id), BusinessErrorMessage.PaymentGatewayUnavailable));
            }

            // One capturable surface per order: an occurrence the customer began paying on the other
            // channel is finished there, as ResumeOrderCheckout refuses a session beside a PaymentIntent.
            var otherChannelSurface = channelProvider.Channel == OrderChannel.Web
                ? order.StripePaymentIntentId
                : order.StripeSessionId;
            if (!string.IsNullOrEmpty(otherChannelSurface))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.Id), BusinessErrorMessage.InvalidOrderStatusTransition));
            }

            // Card flow mirrors CreatePaymentIntent.Handler: ensure the user
            // has a Stripe Customer, create / reuse a PaymentIntent for the
            // order, generate a fresh ephemeral key per request. The payment
            // status doesn't change here — the Stripe webhook marks it Paid
            // once payment succeeds.
            var user = await userRepository.GetByIdAsync(sessionUserId, cancellationToken);
            if (user == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.UserId), BusinessErrorMessage.UserNotFound));
            }

            // A recurring occurrence IS "the next order". GetMyCredit tells every customer their
            // balance applies automatically to it, and until this line that was only true of one-off
            // bookings — a customer on a weekly plan would have watched a balance sit there forever.
            //
            // Same two steps and same order as CreateOrder: take it first so the conditional UPDATE
            // arbitrates, then price the order from what was actually taken. The intent mints its
            // amount from order.AmountDueOnCard below, so applying it here is what makes the charge
            // smaller. Guarded on CreditAppliedAmount so a re-POSTed confirmation — the customer
            // backgrounded the app and came back — cannot take a second bite; the debit's own
            // order-id key is the backstop underneath that.
            if (order.CreditAppliedAmount <= 0m)
            {
                var credit = await TakeCreditForOrderAsync(order, sessionUserId, cancellationToken);
                if (credit > 0m)
                {
                    order.ApplyCredit(credit, sessionUserId);
                }
            }

            order.ConfirmByCustomer(DateTime.UtcNow);

            if (channelProvider.Channel == OrderChannel.Web)
            {
                return await StartCheckoutAsync(order, cancellationToken);
            }

            var stripeCustomerId = user.StripeCustomerId;
            if (string.IsNullOrEmpty(stripeCustomerId))
            {
                stripeCustomerId = await stripeClient.CreateCustomerAsync(
                    user.Id,
                    user.Email,
                    $"{user.FirstName} {user.LastName}".Trim(),
                    user.PhoneNumber,
                    cancellationToken);
                user.AssignStripeCustomerId(stripeCustomerId);
                logger.LogInformation(
                    "Created Stripe customer {StripeCustomerId} for user {UserId}",
                    stripeCustomerId, user.Id);
            }

            // AmountDueOnCard on every charge surface without exception. Recurring orders never
            // carry credit today (CreateOrder is the only path that applies it), so this is identical
            // to TotalPrice right now - and it is written this way so that when they do, the third
            // charge surface is not the one that quietly charges the card twice.
            var intent = await stripeClient.CreatePaymentIntentAsync(
                amount: order.AmountDueOnCard,
                currency: order.Currency?.Code
                          ?? throw new InvalidOperationException(
                              $"Order {order.Id} has no resolved currency; a payment intent cannot be denominated."),
                stripeCustomerId: stripeCustomerId,
                orderId: order.Id,
                displayOrderNumber: order.DisplayOrderNumber,
                cancellationToken: cancellationToken);

            if (string.IsNullOrEmpty(order.StripePaymentIntentId))
            {
                order.AssignStripePaymentIntentId(intent.Id);
            }

            var ephemeralKey = await stripeClient.CreateEphemeralKeyAsync(
                stripeCustomerId, cancellationToken);

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                ClientSecret: intent.ClientSecret,
                PaymentIntentId: intent.Id,
                StripeCustomerId: stripeCustomerId,
                EphemeralKey: ephemeralKey));
        }

        /// <summary>
        /// The web has no PaymentSheet, so it pays through a Checkout Session, as a web booking does.
        ///
        /// <para>The session closes no later than the cutoff at which AutoCancelStaleRecurringOrders
        /// retracts an unpaid occurrence, so nobody can pay for an occurrence that is gone. Stripe keeps a
        /// session open for 30 minutes to 24 hours, so the expiry steps back from that cutoff in 23-hour
        /// strides to the first one still ahead. Every confirm inside one stride names the same expiry, and
        /// so the same idempotency key, and gets the same session back rather than a second surface; once
        /// it has closed, the next stride opens the next. In a stride's last half hour Stripe will not open
        /// a session that short, so the next stride is used — but a session already open for this stride is
        /// replayed first, because a replay is not checked against the time left.</para>
        /// </summary>
        private async Task<BusinessResult<Response>> StartCheckoutAsync(Order order, CancellationToken cancellationToken)
        {
            var cutoff = DateTime.SpecifyKind(order.CleaningDateTime, DateTimeKind.Utc)
                .AddHours(-AutoCancelStaleRecurringOrders.DefaultMissedConfirmGraceHours);
            var strides = Math.Floor((cutoff - DateTime.UtcNow) / CheckoutStride);
            var expiresAt = cutoff - strides * CheckoutStride;

            try
            {
                CheckoutSessionResult session;
                try
                {
                    session = await stripeClient.CreateCheckoutSessionAsync(order, expiresAt, cancellationToken);
                }
                catch (StripeException ex) when (ex.StripeError?.Param == "expires_at" && strides > 0)
                {
                    session = await stripeClient.CreateCheckoutSessionAsync(
                        order, expiresAt + CheckoutStride, cancellationToken);
                }

                order.AssignStripeSessionId(session.Id);

                return BusinessResult.Success(new Response(
                    OrderId: order.Id,
                    ClientSecret: null,
                    PaymentIntentId: null,
                    StripeCustomerId: null,
                    EphemeralKey: null,
                    CheckoutUrl: session.Url));
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Checkout for recurring order {OrderId} could not be started", order.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.Id), BusinessErrorMessage.PaymentGatewayUnavailable));
            }
        }
    }
}
