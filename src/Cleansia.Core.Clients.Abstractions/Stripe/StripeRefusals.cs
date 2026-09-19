using global::Stripe;

namespace Cleansia.Core.Clients.Abstractions.Stripe;

/// <summary>
/// The Stripe refusals a handler turns into a named business error rather than the generic
/// gateway-unavailable answer. Pure predicates over the exception Stripe.net raises.
/// </summary>
public static class StripeRefusals
{
    /// <summary>
    /// Stripe's per-Customer currency rule: once a Customer has been invoiced in one currency, Stripe
    /// may refuse a subscription in another on the same Customer. Stripe documents the refusal by its
    /// message ("You cannot combine currencies on a single customer") and gives it no dedicated
    /// <c>StripeError.Code</c>; the sandbox probe of 2026-09-13 found the rule NOT enforced for a
    /// Customer whose only other-currency subscription was cancelled, so this branch is kept for the
    /// documented rule and matches the documented wording.
    /// </summary>
    public static bool IsCustomerCurrencyLocked(StripeException exception)
    {
        var message = exception.StripeError?.Message ?? exception.Message;
        return message.Contains("combine currencies", StringComparison.OrdinalIgnoreCase);
    }
}
