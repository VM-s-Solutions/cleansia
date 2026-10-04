using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Orders;

public class CreatePaymentIntent
{
    /// <param name="SaveCard">
    /// The customer ticked "save this card for my next bookings": Stripe keeps the card for off-session use
    /// and a saved card is recorded under the consent, completed when the payment succeeds. Unticked, nothing
    /// is kept.
    /// </param>
    public record Command(string OrderId, bool SaveCard = false) : ICommand<Response>;

    public record Response(
        string ClientSecret,
        string PaymentIntentId,
        string StripeCustomerId,
        string EphemeralKey);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            IOrderRepository orderRepository,
            IUserSessionProvider userSessionProvider)
        {
            _orderRepository = orderRepository;
            _userSessionProvider = userSessionProvider;

            // Returning OrderNotFound for non-owners is deliberate — it
            // prevents enumeration of which order ids exist for other users.
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(BeOwnedByCallerAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .MustAsync(BeCardPaymentAsync)
                .WithMessage(BusinessErrorMessage.InvalidEnumValue)
                .MustAsync(NotAlreadyPaidAsync)
                .WithMessage(BusinessErrorMessage.OrderPaymentAlreadyPaid)
                .MustAsync(NotAwaitingCustomerConfirmationAsync)
                .WithMessage(BusinessErrorMessage.InvalidOrderStatusTransition);
        }

        private async Task<bool> BeOwnedByCallerAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await LoadOwnOrderAsync(orderId, cancellationToken);
            return order != null && order.UserId == _userSessionProvider.GetUserId();
        }

        private async Task<bool> BeCardPaymentAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await LoadOwnOrderAsync(orderId, cancellationToken);
            return order != null && order.PaymentType == PaymentType.Card;
        }

        private async Task<bool> NotAlreadyPaidAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await LoadOwnOrderAsync(orderId, cancellationToken);
            return order != null && order.PaymentStatus != PaymentStatus.Paid;
        }

        // An occurrence is paid once the customer confirms it, and ConfirmRecurringOrder is where the terms in
        // force are asked for; paying here first would step around that.
        private async Task<bool> NotAwaitingCustomerConfirmationAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await LoadOwnOrderAsync(orderId, cancellationToken);
            return order != null && (order.RecurringTemplateId == null || order.CustomerConfirmedAt != null);
        }

        // The caller's own order in whichever operating company the market put it (S8: pinned by the
        // caller's own id); null for anyone else's, so a stranger's order and a missing one read alike.
        private Task<Order?> LoadOwnOrderAsync(string orderId, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            return string.IsNullOrEmpty(userId)
                ? Task.FromResult<Order?>(null)
                : _orderRepository.GetByIdForOwnerAsync(orderId, userId, cancellationToken);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IStripeClient stripeClient,
        IStripeConfig stripeConfig,
        IStripeCustomerResolver stripeCustomerResolver,
        ISavedCardRepository savedCardRepository,
        IRequestMetadataProvider requestMetadataProvider,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Ownership, payment type, not paid and a confirmed occurrence are enforced by the Validator.
            var order = (await orderRepository.GetByIdForOwnerAsync(
                command.OrderId, userSessionProvider.GetUserId()!, cancellationToken))!;

            // The mobile charge surface. Gated with the other two, and BEFORE CreateCustomerAsync so a
            // refused payment leaves no Stripe customer behind. -> IStripeConfig
            if (!stripeConfig.Enabled)
            {
                logger.LogWarning(
                    "PaymentSheet intent refused for order {OrderId}: card payments are disabled (Stripe:Enabled=false)",
                    order.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.PaymentGatewayUnavailable));
            }
            var sessionUserId = userSessionProvider.GetUserId()!;

            var user = await userRepository.GetByIdAsync(sessionUserId, cancellationToken);
            if (user == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.UserNotFound));
            }

            var currency = order.Currency
                           ?? throw new InvalidOperationException(
                               $"Order {order.Id} has no resolved currency; a payment intent cannot be denominated.");

            string stripeCustomerId;
            SavedCard? cardToSave = null;
            if (command.SaveCard)
            {
                // A card kept for later sits on the customer's Stripe Customer for its currency, where the
                // profile's card capture puts one too.
                stripeCustomerId = await stripeCustomerResolver.ResolveForCurrencyAsync(user, currency, cancellationToken);
                cardToSave = SavedCard.Start(
                    user.Id, currency.Id, stripeCustomerId, requestMetadataProvider.IpAddress, requestMetadataProvider.DeviceLabel);
            }
            else
            {
                stripeCustomerId = user.StripeCustomerId ?? string.Empty;
                if (string.IsNullOrEmpty(stripeCustomerId))
                {
                    stripeCustomerId = await stripeClient.CreateCustomerAsync(
                        user.Id,
                        user.Email,
                        $"{user.FirstName} {user.LastName}".Trim(),
                        user.PhoneNumber,
                        cancellationToken);
                    user.AssignStripeCustomerId(stripeCustomerId);
                    logger.LogInformation("Created Stripe customer for user {UserId}", user.Id);
                }
            }

            // AmountDueOnCard, not TotalPrice - see StripeClient.CreateCheckoutSessionAsync. Re-opening the
            // sheet gets the order's open intent back; a changed amount or tick gets a new one, and the old
            // one is cancelled through the branch below.
            var intent = await stripeClient.CreatePaymentIntentAsync(
                amount: order.AmountDueOnCard,
                currency: currency.Code,
                stripeCustomerId: stripeCustomerId,
                orderId: order.Id,
                displayOrderNumber: order.DisplayOrderNumber,
                savedCardId: cardToSave?.Id,
                currentPaymentIntentId: order.StripePaymentIntentId,
                cancellationToken: cancellationToken);

            if (cardToSave is not null && intent.Id != order.StripePaymentIntentId)
            {
                savedCardRepository.Add(cardToSave);
            }

            if (string.IsNullOrEmpty(order.StripePaymentIntentId))
            {
                order.AssignStripePaymentIntentId(intent.Id);
            }
            else if (order.StripePaymentIntentId != intent.Id)
            {
                // We must cancel the OLD intent so the customer can't end up
                // paying both. It is cancelled as a duplicate, which the
                // payment_intent.canceled webhook leaves alone, since the
                // customer is about to pay the new one.
                var oldIntentId = order.StripePaymentIntentId;
                try
                {
                    await stripeClient.CancelReplacedPaymentIntentAsync(oldIntentId, cancellationToken);
                    logger.LogInformation(
                        "Cancelled stale PaymentIntent {OldIntentId} for order {OrderId}; new intent is {NewIntentId}",
                        oldIntentId, order.Id, intent.Id);
                }
                catch (StripeException ex)
                {
                    // If the old intent is already succeeded, cancel throws.
                    // That's the dangerous case — customer may have just paid
                    // the old intent and we're about to hand them a new one.
                    // Refuse to proceed; webhook reconciliation will catch up.
                    logger.LogError(ex,
                        "Failed to cancel stale PaymentIntent {OldIntentId} for order {OrderId}; refusing to mint a new intent to avoid double-charge",
                        oldIntentId, order.Id);
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(order.PaymentType),
                        BusinessErrorMessage.PaymentGatewayUnavailable));
                }
                order.AssignStripePaymentIntentId(intent.Id);
            }

            var ephemeralKey = await stripeClient.CreateEphemeralKeyAsync(
                stripeCustomerId, cancellationToken);

            return BusinessResult.Success(new Response(
                ClientSecret: intent.ClientSecret,
                PaymentIntentId: intent.Id,
                StripeCustomerId: stripeCustomerId,
                EphemeralKey: ephemeralKey));
        }
    }
}
