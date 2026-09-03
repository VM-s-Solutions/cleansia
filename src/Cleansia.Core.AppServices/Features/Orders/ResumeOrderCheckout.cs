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

/// <summary>
/// A second run at paying an order the customer walked away from.
///
/// <para>The web card flow creates the order BEFORE the redirect to Stripe, so backing out of
/// Checkout leaves a real, unpaid booking rather than nothing — and
/// <see cref="CleanupStalePendingOrders"/> cancels it an hour later. Until then the customer has
/// something to pay for and, before this, no way to pay it: the only thing that ever minted a
/// Checkout Session was <see cref="CreateOrder"/>, and the only "try again" the cancel page offered
/// was the booking wizard from a blank slate.</para>
///
/// <para>ONE CAPTURABLE SURFACE, unchanged. This does not mint a second session: Stripe replays an
/// idempotent request for 24 hours and <c>StripeClient</c> keys session creation on
/// <c>checkout-{orderId}</c>, so asking again inside that window returns the SAME session the
/// customer abandoned. Two live sessions against one order is precisely the shape
/// <c>OrderPaymentDispatcher</c> suppresses on the mobile channel, and it is not introduced here.</para>
///
/// <para>Authenticated only, like every other mutation of an existing order on this controller. The
/// web card checkout itself is anonymous, so a guest who abandons payment cannot reach this — that
/// is a deliberate limit rather than an oversight, because the alternative is a write authorised by
/// possession of an order id, which nothing in this API does today.</para>
/// </summary>
public class ResumeOrderCheckout
{
    public record Command(string OrderId) : ICommand<Response>;

    public record Response(string CheckoutUrl);

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

            // Ordering is load-bearing and the cascade stops: OrderNotFound has to answer for a
            // non-owner before any later rule can report the order's payment state, or this becomes
            // an oracle for whether an id exists and whether it has been paid.
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
                .MustAsync(NotCancelledAsync)
                .WithMessage(BusinessErrorMessage.OrderAlreadyCancelled)
                .MustAsync(HaveNoPaymentIntentAsync)
                .WithMessage(BusinessErrorMessage.InvalidOrderStatusTransition);
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
            // Paid, Refunded, Disputed and PartiallyRefunded all describe money that has already
            // moved. Only a Pending or Failed order has anything left to collect.
            return order != null
                && order.PaymentStatus is PaymentStatus.Pending or PaymentStatus.Failed;
        }

        private async Task<bool> NotCancelledAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
            return order != null && order.CurrentStatus != OrderStatus.Cancelled;
        }

        /// <summary>
        /// A mobile order that opened PaymentSheet holds a PaymentIntent, which is its single
        /// capturable surface — <c>OrderPaymentDispatcher</c> suppresses the Checkout Session there
        /// for exactly that reason. Handing that order a session as well would give one booking two
        /// independent ways to be charged.
        /// </summary>
        private async Task<bool> HaveNoPaymentIntentAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
            return order != null && string.IsNullOrEmpty(order.StripePaymentIntentId);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IStripeClientFactory stripeClientFactory,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Ownership, payment type, payment status, order status and the absence of a
            // PaymentIntent are all enforced by the Validator.
            var order = (await orderRepository.GetByIdAsync(command.OrderId, cancellationToken))!;

            try
            {
                var stripeClient = stripeClientFactory.CreateClient();
                var checkoutUrl = await stripeClient.CreateCheckoutSessionAsync(order, cancellationToken);
                return BusinessResult.Success(new Response(checkoutUrl));
            }
            catch (StripeException ex)
            {
                // Narrow catch, matching OrderPaymentDispatcher: only Stripe's own failures map to
                // "gateway unavailable". Anything else should surface as a 500 rather than be
                // dressed up as a transient outage.
                logger.LogError(ex, "Resuming checkout failed for order {OrderId}", order.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.PaymentGatewayUnavailable));
            }
        }
    }
}
