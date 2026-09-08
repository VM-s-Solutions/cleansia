using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Customer confirmation of an occurrence the recurring materializer spawned. Card returns a Stripe
/// PaymentIntent and the order confirms only on the webhook; cash flips to Confirmed and Paid
/// immediately and queues the receipt.
///
/// <para>Refuses orders that are not pending, not owned by the caller, or not linked to a template —
/// those belong on the standard booking flow. → /flows/booking-and-pricing#recurring-bookings</para>
/// </summary>
public class ConfirmRecurringOrder
{
    public record Command(string OrderId) : ICommand<Response>;

    /// <summary>
    /// Both flavors return the same shape; consumers branch on
    /// <see cref="ClientSecret"/>: non-null = Card path, mobile opens
    /// PaymentSheet; null = Cash path, mobile shows success snackbar.
    /// </summary>
    public record Response(
        string OrderId,
        string? ClientSecret,
        string? PaymentIntentId,
        string? StripeCustomerId,
        string? EphemeralKey);

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
        IOrderRepository orderRepository,
        ICreditAccountRepository creditAccountRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IStripeClient stripeClient,
        IStripeConfig stripeConfig,
        IPendingDispatch pending,
        INotificationProducer notificationProducer,
        IPreferredCleanerHoldResolver preferredCleanerHoldResolver,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var sessionUserId = userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(sessionUserId))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
            if (order == null
                || order.UserId != sessionUserId
                || string.IsNullOrEmpty(order.RecurringTemplateId))
            {
                // Hide all three failure modes behind the same NotFound error so
                // the response doesn't leak whether an order id exists.
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            if (order.PaymentStatus != PaymentStatus.Pending)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentStatus), BusinessErrorMessage.OrderPaymentAlreadyPaid));
            }

            return order.PaymentType switch
            {
                PaymentType.Cash => await HandleCashAsync(order, cancellationToken),
                PaymentType.Card => await HandleCardAsync(order, sessionUserId, cancellationToken),
                _ => BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentType), BusinessErrorMessage.InvalidEnumValue)),
            };
        }

        private async Task<BusinessResult<Response>> HandleCashAsync(
            Order order, CancellationToken cancellationToken)
        {
            // Cash means the customer pays the cleaner on-site — no gateway
            // step, the order moves straight to Confirmed. Mirrors the Cash
            // branch in CreateOrder.Handler so receipts get queued the same way.
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
            order.UpdatePaymentStatus(PaymentStatus.Paid);

            pending.Enqueue(
                QueueNames.GenerateReceipt,
                new QueueEnvelope<GenerateReceiptMessage>(
                    MessageKeys.Receipt(order.Id),
                    order.TenantId,
                    new GenerateReceiptMessage(order.Id, Constants.Language.English)),
                MessageKeys.Receipt(order.Id));

            if (!string.IsNullOrEmpty(order.UserId))
            {
                await notificationProducer.NotifyAsync(
                    order.UserId,
                    NotificationEventCatalog.OrderConfirmed,
                    new Dictionary<string, string>
                    {
                        ["orderId"] = order.Id,
                        ["orderNumber"] = order.DisplayOrderNumber,
                    },
                    order.TenantId,
                    order.Id,
                    cancellationToken);
            }

            // Q-BROWSE-01 (b): a recurring occurrence is never offerable at creation — the money term
            // demands Paid for anything carrying a RecurringTemplateId, because AutoCancelStaleRecurring
            // Orders retracts those while they are Pending. The two writes above are that transition, so
            // this is where its preferred cleaner is told. The card flavour is told by the Stripe
            // webhook instead, and the PaymentStatus guard upstream keeps either to one announcement.
            await PreferredOfferNotifier.NotifyBecameOfferableAsync(
                order, preferredCleanerHoldResolver, notificationProducer, DateTime.UtcNow, cancellationToken);

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

            var spendable = await creditAccountRepository.GetSpendableAsync(userId, cancellationToken);
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
            // Card flow mirrors CreatePaymentIntent.Handler: ensure the user
            // has a Stripe Customer, create / reuse a PaymentIntent for the
            // order, generate a fresh ephemeral key per request. Order status
            // doesn't change here — the Stripe webhook is what flips it to
            // Confirmed once payment succeeds.
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
                currency: order.Currency.Code,
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
    }
}
