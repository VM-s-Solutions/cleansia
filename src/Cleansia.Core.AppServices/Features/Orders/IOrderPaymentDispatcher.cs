using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Owns the payment-type side-effect concern lifted out of <see cref="CreateOrder.Handler"/>: the
/// <c>switch (PaymentType)</c> that, for Card, creates a Stripe checkout session, and for Cash, records
/// intent to send the booking e-mail at the post-commit dispatch seam.
///
/// The contract preserves the handler's original semantics exactly:
///   * <b>Card</b> creates a Stripe checkout session; a narrow <c>StripeException</c> maps to a
///     <c>PaymentGatewayUnavailable</c> failure (anything else bubbles as a 500), and never enqueues;
///   * <b>Cash</b> never creates a Stripe session and records the booking e-mail intent via
///     <see cref="Core.Queue.Abstractions.IPendingDispatch"/> — the ADR-0002 post-commit dispatch /
///     outbox seam, dispatched only after the order row is durably committed.
/// </summary>
public interface IOrderPaymentDispatcher
{
    /// <param name="saveCard">
    /// The customer ticked "save this card": on the web the Checkout Session asks Stripe to keep the card and
    /// a saved card is recorded under the consent. The mobile channel saves at its PaymentSheet intent instead.
    /// </param>
    Task<OrderPaymentDispatchResult> DispatchAsync(
        Order order, string languageCode, bool saveCard, CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of <see cref="IOrderPaymentDispatcher.DispatchAsync"/>: either the Card flow's Stripe
/// Checkout URL (null for Cash) or the <see cref="Error"/> the handler returns as a failure.
///
/// <para>Named for what it carries. It was <c>StripeSessionId</c> and has always held the browser
/// redirect URL, which was survivable while the id was thrown away and is not now that the order
/// records the id as its charge surface.</para>
/// </summary>
public record OrderPaymentDispatchResult(string? CheckoutUrl, Error? Failure)
{
    public static OrderPaymentDispatchResult Ok(string? checkoutUrl) => new(checkoutUrl, null);
    public static OrderPaymentDispatchResult Fail(Error error) => new(null, error);
}
