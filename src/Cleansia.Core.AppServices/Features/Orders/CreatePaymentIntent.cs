using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Orders;

public class CreatePaymentIntent
{
    public record Command(string OrderId) : ICommand<Response>;

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
                .WithMessage("order.payment.already_paid");
        }

        private async Task<bool> BeOwnedByCallerAsync(string orderId, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId)) return false;
            var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
            return order != null && order.UserId == userId;
        }

        private async Task<bool> BeCardPaymentAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
            return order != null && order.PaymentType == PaymentType.Card;
        }

        private async Task<bool> NotAlreadyPaidAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
            return order != null && order.PaymentStatus != PaymentStatus.Paid;
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IStripeClient stripeClient,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Ownership + payment-type + not-paid enforced by Validator.
            var order = (await orderRepository.GetByIdAsync(command.OrderId, cancellationToken))!;
            var sessionUserId = userSessionProvider.GetUserId()!;

            var user = await userRepository.GetByIdAsync(sessionUserId, cancellationToken);
            if (user == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.UserNotFound));
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
                logger.LogInformation("Created Stripe customer for user {UserId}", user.Id);
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
            else if (order.StripePaymentIntentId != intent.Id)
            {
                // Amount changed (typically: customer edited extras after
                // first PaymentSheet open). Stripe's idempotency key includes
                // the cents amount, so a different amount → different intent.
                // We must cancel the OLD intent so the customer can't end up
                // paying both — best-effort, log on failure (stale intent
                // will eventually be garbage-collected by Stripe but until
                // then it's a double-charge risk).
                var oldIntentId = order.StripePaymentIntentId;
                try
                {
                    await stripeClient.CancelPaymentIntentAsync(oldIntentId, cancellationToken);
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
