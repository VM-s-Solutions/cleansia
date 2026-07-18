using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
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
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Customer-driven confirmation of a Pending order spawned by the recurring
/// materializer. Two flavors based on the template's stored payment type:
///
///   * <see cref="PaymentType.Card"/> — returns a Stripe PaymentIntent so the
///     mobile PaymentSheet can collect payment. Mirrors
///     <see cref="CreatePaymentIntent"/>'s behaviour exactly; the order
///     transitions to Confirmed only after the payment-success webhook.
///   * <see cref="PaymentType.Cash"/> — flips the order to Confirmed +
///     PaymentStatus.Paid immediately and queues receipt generation, since
///     no payment gateway is involved.
///
/// Refuses orders that are not Pending, not owned by the caller, or not
/// linked to a recurring template — those go through the standard booking
/// flow rather than this confirm path.
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
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IStripeClient stripeClient,
        IPendingDispatch pending,
        INotificationProducer notificationProducer,
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
                    nameof(order.PaymentStatus), "order.payment.already_paid"));
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

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                ClientSecret: null,
                PaymentIntentId: null,
                StripeCustomerId: null,
                EphemeralKey: null));
        }

        private async Task<BusinessResult<Response>> HandleCardAsync(
            Order order, string sessionUserId, CancellationToken cancellationToken)
        {
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

            var intent = await stripeClient.CreatePaymentIntentAsync(
                amount: order.TotalPrice,
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
