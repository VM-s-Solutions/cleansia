namespace Cleansia.Core.Domain.Loyalty;

/// <summary>
/// The per-tenant lifetime-points thresholds that decide a <see cref="LoyaltyTier"/>, resolved from the
/// admin-editable <see cref="LoyaltyTierConfig"/> rows and handed to <see cref="LoyaltyAccount"/> so the
/// domain stays persistence-ignorant. A tier whose config row is missing must be given
/// <see cref="int.MaxValue"/> so it is unreachable and resolution degrades to the next tier down.
/// </summary>
public sealed record LoyaltyTierThresholds(int Silver, int Gold, int Platinum)
{
    public static LoyaltyTierThresholds From(IEnumerable<LoyaltyTierConfig> configs)
    {
        var byTier = configs.ToList();
        return new LoyaltyTierThresholds(
            Silver: ThresholdOf(LoyaltyTier.SilverMopper),
            Gold: ThresholdOf(LoyaltyTier.GoldPolisher),
            Platinum: ThresholdOf(LoyaltyTier.PlatinumSparkler));

        int ThresholdOf(LoyaltyTier tier) =>
            byTier.FirstOrDefault(c => c.Tier == tier)?.LifetimePointsThreshold ?? int.MaxValue;
    }

    public LoyaltyTier ResolveTier(int lifetimePoints)
    {
        if (lifetimePoints >= Platinum)
        {
            return LoyaltyTier.PlatinumSparkler;
        }

        if (lifetimePoints >= Gold)
        {
            return LoyaltyTier.GoldPolisher;
        }

        if (lifetimePoints >= Silver)
        {
            return LoyaltyTier.SilverMopper;
        }

        return LoyaltyTier.BronzeCleaner;
    }
}
