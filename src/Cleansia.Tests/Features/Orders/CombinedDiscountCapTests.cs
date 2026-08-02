using System.Reflection;
using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Pins the 12% combined (Plus + loyalty tier) ceiling and the promo replacement rule.
///
/// The ceiling is an OWNER RULING, not a tuning constant: the top tier is already 12%, so letting the
/// 5% Plus rate stack on it un-capped would be a 17% discount, which the owner judged too much. It
/// reads like a bug — a paying subscriber whose Plus discount is worth nothing — so it invites being
/// "fixed" back. It is not a bug.
///
/// The member-facing copy on web, Android and iOS now states these two consequences in five locales
/// (membership_perk_discount_* / membership_social_proof_*), so these are also the assertions that
/// make that copy true. Change the arithmetic and the copy is false.
/// </summary>
public class CombinedDiscountCapTests
{
    private const decimal Subtotal = 1000m;
    private const decimal PlusRate = 0.05m;
    private const decimal TopTierRate = 0.12m;
    private const decimal GoldTierRate = 0.10m;

    [Fact]
    public void The_combined_ceiling_is_twelve_percent()
    {
        Assert.Equal(0.12m, OrderFactory.MaxCombinedDiscountFraction);
    }

    /// <summary>At the top tier the ceiling equals the tier rate, so Plus adds nothing. This is the
    /// accepted consequence of the owner ruling — the copy must not promise otherwise.</summary>
    [Fact]
    public void At_the_top_tier_plus_adds_nothing_on_top()
    {
        var tierOnly = Resolve(membership: 0m, tier: Subtotal * TopTierRate);
        var tierWithPlus = Resolve(membership: Subtotal * PlusRate, tier: Subtotal * TopTierRate);

        Assert.Equal(tierOnly.Total, tierWithPlus.Total);
        Assert.Equal(Subtotal * OrderFactory.MaxCombinedDiscountFraction, tierWithPlus.Total);
    }

    /// <summary>Below the top tier Plus is worth only the distance left to the ceiling — two points
    /// here, not its nominal five.</summary>
    [Fact]
    public void Below_the_top_tier_plus_is_worth_only_the_distance_to_the_ceiling()
    {
        var tierOnly = Resolve(membership: 0m, tier: Subtotal * GoldTierRate);
        var tierWithPlus = Resolve(membership: Subtotal * PlusRate, tier: Subtotal * GoldTierRate);

        Assert.Equal(Subtotal * 0.02m, tierWithPlus.Total - tierOnly.Total);
    }

    /// <summary>Far enough below the ceiling, Plus is worth its full rate — the claim is true here,
    /// which is why the copy states the ceiling rather than dropping the rate.</summary>
    [Fact]
    public void With_no_tier_discount_plus_is_worth_its_full_rate()
    {
        var resolution = Resolve(membership: Subtotal * PlusRate, tier: 0m);

        Assert.Equal(Subtotal * PlusRate, resolution.Total);
        Assert.Equal(Subtotal * PlusRate, resolution.Membership);
    }

    /// <summary>Promo does not stack — the larger of (promo) and (Plus + tier) applies, and the other
    /// side goes to zero.</summary>
    [Fact]
    public void A_larger_promo_replaces_the_plus_and_tier_discount_entirely()
    {
        var resolution = Resolve(membership: Subtotal * PlusRate, tier: Subtotal * GoldTierRate, promo: Subtotal * 0.20m);

        Assert.Equal(Subtotal * 0.20m, resolution.Total);
        Assert.Equal(0m, resolution.Membership);
        Assert.Equal(0m, resolution.Tier);
    }

    [Fact]
    public void A_smaller_promo_is_dropped_in_favour_of_the_plus_and_tier_discount()
    {
        var resolution = Resolve(membership: Subtotal * PlusRate, tier: Subtotal * GoldTierRate, promo: Subtotal * 0.03m);

        Assert.Equal(0m, resolution.Promo);
        Assert.Equal(Subtotal * OrderFactory.MaxCombinedDiscountFraction, resolution.Total);
    }

    private sealed record Resolution(decimal Membership, decimal Tier, decimal Promo, decimal Total);

    private static Resolution Resolve(decimal membership, decimal tier, decimal promo = 0m)
    {
        var method = typeof(OrderFactory).GetMethod(
            "ResolveLoy003Discount", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(method);

        var result = method!.Invoke(null, [membership, tier, promo, Subtotal])!;
        var type = result.GetType();
        decimal Read(string name) => (decimal)type.GetProperty(name)!.GetValue(result)!;

        return new Resolution(
            Read("MembershipAmount"), Read("TierAmount"), Read("PromoAmount"), Read("TotalAmount"));
    }
}
