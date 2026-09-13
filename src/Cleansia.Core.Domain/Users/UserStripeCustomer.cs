using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Users;

/// <summary>
/// The Stripe Customer that bills this user in ONE currency. Stripe locks a Customer to the currency
/// of its first invoice and refuses a subscription in another, so a customer whose CZK Plus was
/// cancelled could never subscribe in EUR on the one Customer <see cref="User.StripeCustomerId"/>
/// holds. One row per (user, currency) is the mechanism that keeps re-subscribing possible (owner
/// ruling 2026-09-13). The legacy field stays: it is still the Customer one-off order
/// payments use, and the first row for a currency it has only ever billed adopts it.
/// </summary>
public class UserStripeCustomer : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(26)]
    public string UserId { get; private set; } = default!;
    public User? User { get; private set; }

    [Required]
    [MaxLength(26)]
    public string CurrencyId { get; private set; } = default!;
    public Currency? Currency { get; private set; }

    [Required]
    [MaxLength(64)]
    public string StripeCustomerId { get; private set; } = default!;

    public static UserStripeCustomer Create(string userId, string currencyId, string stripeCustomerId) => new()
    {
        UserId = userId,
        CurrencyId = currencyId,
        StripeCustomerId = stripeCustomerId,
    };
}
