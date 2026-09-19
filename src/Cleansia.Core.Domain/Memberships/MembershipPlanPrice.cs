using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Memberships;

/// <summary>
/// What one billing period of a <see cref="MembershipPlan"/> costs in ONE currency, and which Stripe
/// Price charges it. Authored per currency exactly like <c>PackagePrice</c> — never converted — and a
/// plan with no row in a currency is not on sale in that market. → /decisions/adr-0059
/// </summary>
public class MembershipPlanPrice : Auditable
{
    [Required]
    public string MembershipPlanId { get; private set; } = default!;
    public MembershipPlan? MembershipPlan { get; private set; }

    [Required]
    public string CurrencyId { get; private set; } = default!;
    public Currency? Currency { get; private set; }

    /// <summary>One billing period of the plan in this currency — the full annual charge for a yearly plan.</summary>
    public decimal Price { get; private set; }

    /// <summary>The Stripe Price in THIS currency for THIS plan. A Stripe Price is single-currency, so it belongs to exactly one row.</summary>
    [Required]
    [MaxLength(64)]
    public string StripePriceId { get; private set; } = default!;

    public static MembershipPlanPrice Create(string membershipPlanId, string currencyId, decimal price, string stripePriceId) => new()
    {
        MembershipPlanId = membershipPlanId,
        CurrencyId = currencyId,
        Price = price,
        StripePriceId = stripePriceId,
    };

    public MembershipPlanPrice Update(decimal price, string stripePriceId)
    {
        Price = price;
        StripePriceId = stripePriceId;
        return this;
    }
}
