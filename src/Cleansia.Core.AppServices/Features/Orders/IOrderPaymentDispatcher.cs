using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Owns the payment-type side-effect concern lifted out of <see cref="CreateOrder.Handler"/>: the
/// <c>switch (PaymentType)</c> that, for Card, creates a Stripe checkout session, and for Cash, records
/// intent to generate the receipt at the post-commit dispatch seam.
///
/// The contract preserves the handler's original semantics exactly:
///   * <b>Card</b> creates a Stripe checkout session; a narrow <c>StripeException</c> maps to a
///     <c>PaymentGatewayUnavailable</c> failure (anything else bubbles as a 500), and never enqueues;
///   * <b>Cash</b> never creates a Stripe session and records the receipt intent via
///     <see cref="Core.Queue.Abstractions.IPendingDispatch"/> — the ADR-0002 post-commit dispatch /
///     outbox seam, dispatched only after the order row is durably committed.
/// </summary>
public interface IOrderPaymentDispatcher
{
    Task<OrderPaymentDispatchResult> DispatchAsync(
        Order order, string languageCode, CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of <see cref="IOrderPaymentDispatcher.DispatchAsync"/>: either the Card flow's Stripe
/// session id (null for Cash) or the <see cref="Error"/> the handler returns as a failure.
/// </summary>
public record OrderPaymentDispatchResult(string? StripeSessionId, Error? Failure)
{
    public static OrderPaymentDispatchResult Ok(string? stripeSessionId) => new(stripeSessionId, null);
    public static OrderPaymentDispatchResult Fail(Error error) => new(null, error);
}
