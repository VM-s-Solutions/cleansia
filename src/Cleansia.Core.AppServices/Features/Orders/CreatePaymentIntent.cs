using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Mobile PaymentSheet entry point: given an existing card-payable order in
/// Pending status, returns the Stripe PaymentIntent client_secret + ephemeral
/// key + customer id that the mobile SDK needs to confirm payment.
///
/// Lazily provisions a Stripe Customer on the user's first card payment and
/// persists the id back to <see cref="Cleansia.Core.Domain.Users.User.StripeCustomerId"/>
/// so subsequent bookings reuse it (enabling saved cards in PaymentSheet).
/// </summary>
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
        IStripeClient stripeClient,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            // Card-only path. Cash orders never get a PaymentIntent — they
            // settle off-platform and the order's PaymentStatus moves manually.
            if (order.PaymentType != PaymentType.Card)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentType),
                    BusinessErrorMessage.InvalidEnumValue));
            }

            // Already paid → no-op. Returning success would double-charge if
            // the client retries; failing surfaces the right error in the UI.
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentStatus),
                    "order.payment.already_paid"));
            }

            // Owner check. PaymentIntent is created against the user's Stripe
            // customer, so we can't issue one for guest orders. (Guest checkout
            // path uses Checkout Session, not PaymentSheet.)
            if (string.IsNullOrEmpty(order.UserId))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.UserId),
                    BusinessErrorMessage.Required));
            }

            var user = await userRepository.GetByIdAsync(order.UserId, cancellationToken);
            if (user == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.UserId),
                    BusinessErrorMessage.UserNotFound));
            }

            // Lazy Stripe Customer creation. Cash-only users never reach this
            // code path so they never get a Stripe customer record.
            var stripeCustomerId = user.StripeCustomerId;
            if (string.IsNullOrEmpty(stripeCustomerId))
            {
                stripeCustomerId = await stripeClient.CreateCustomerAsync(
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

            // Ephemeral key is consumed once by PaymentSheet to surface saved
            // cards. Lifetime is ~10 minutes; generate fresh per request rather
            // than caching server-side.
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
