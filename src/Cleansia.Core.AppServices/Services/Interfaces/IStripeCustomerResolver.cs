using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The Stripe Customer a subscription in <paramref name="currency"/> is created on, for both
/// subscribe surfaces (owner ruling 2026-09-13). Stripe locks a Customer to the
/// currency of its first invoice, so a user holds one Customer PER CURRENCY (<see cref="UserStripeCustomer"/>):
/// <list type="number">
/// <item>a row for (user, currency) — that Customer;</item>
/// <item>no row, and the legacy <see cref="User.StripeCustomerId"/> has only ever billed this
/// currency or nothing (no membership in another currency, not already claimed by a row) — it is
/// adopted: the row is written and the legacy Customer used;</item>
/// <item>otherwise a new Customer is created at Stripe and the row written.</item>
/// </list>
/// The legacy field is still written when a Customer is created for a user who has none, as every
/// one-off payment path does. A Stripe failure propagates as <c>StripeException</c> for the caller's
/// own classification; the row is added to the unit of work, never flushed here.
/// </summary>
public interface IStripeCustomerResolver
{
    Task<string> ResolveForCurrencyAsync(User user, Currency currency, CancellationToken cancellationToken);
}
