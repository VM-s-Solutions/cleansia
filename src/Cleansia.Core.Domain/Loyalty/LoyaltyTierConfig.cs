using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Loyalty;

/// <summary>
/// Per-tenant configuration for a single <see cref="LoyaltyTier"/>. One row per
/// tier per tenant — seeded on tenant creation, editable by admin (Phase L4).
/// </summary>
public class LoyaltyTierConfig : Auditable, ITenantEntity
{
    [Required]
    public LoyaltyTier Tier { get; private set; }

    [Required]
    public int LifetimePointsThreshold { get; private set; }

    /// <summary>
    /// Discount as a fraction in [0, 1]. e.g. 0.05m = 5%.
    /// </summary>
    [Required]
    public decimal DiscountPercent { get; private set; }

    /// <summary>
    /// Minimum raw subtotal for the tier discount to apply, denominated in the PLATFORM DEFAULT currency
    /// — and enforced only on an order in that currency. On any other currency no floor applies: the
    /// discount is the promise, the floor only keeps it off trivially small orders, and comparing this
    /// number against a subtotal in a stronger currency withheld the promise from a whole market. Null
    /// means no floor anywhere. → /product/business-rules#money-constants
    /// </summary>
    public decimal? MinimumOrderAmountForDiscount { get; private set; }

    /// <summary>
    /// Serialized JSON array of tier perks for the UI. Shape:
    /// [{ "icon": "...", "labelKey": "loyalty.perks..." }, ...]
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string PerksJson { get; private set; } = "[]";

    // Private constructor for EF Core
    private LoyaltyTierConfig() { }

    public static LoyaltyTierConfig Create(
        LoyaltyTier tier,
        int lifetimePointsThreshold,
        decimal discountPercent,
        decimal? minimumOrderAmountForDiscount,
        string perksJson)
    {
        return new LoyaltyTierConfig
        {
            Tier = tier,
            LifetimePointsThreshold = lifetimePointsThreshold,
            DiscountPercent = discountPercent,
            MinimumOrderAmountForDiscount = minimumOrderAmountForDiscount,
            PerksJson = perksJson ?? "[]",
        };
    }

    public LoyaltyTierConfig Update(
        int lifetimePointsThreshold,
        decimal discountPercent,
        decimal? minimumOrderAmountForDiscount,
        string perksJson)
    {
        LifetimePointsThreshold = lifetimePointsThreshold;
        DiscountPercent = discountPercent;
        MinimumOrderAmountForDiscount = minimumOrderAmountForDiscount;
        PerksJson = perksJson ?? "[]";
        return this;
    }

    /// <summary>
    /// Admin edit overload — same as <see cref="Update(int, decimal, decimal?, string)"/>
    /// but stamps the auditing fields with <paramref name="actorId"/>.
    /// </summary>
    public LoyaltyTierConfig Update(
        int lifetimePointsThreshold,
        decimal discountPercent,
        decimal? minimumOrderAmountForDiscount,
        string perksJson,
        string actorId)
    {
        Update(lifetimePointsThreshold, discountPercent, minimumOrderAmountForDiscount, perksJson);
        Updated(actorId, DateTimeOffset.UtcNow);
        return this;
    }
}
