using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Tests.Features.Loyalty;

public class LoyaltyTierThresholdResolutionTests
{
    private const string UserId = "user-1";
    private const string ActorId = "admin-1";

    [Fact]
    public void RecomputeTier_UsesConfiguredThresholds_NotLegacyLiterals()
    {
        var configured = new LoyaltyTierThresholds(Silver: 300, Gold: 1500, Platinum: 4000);
        var account = LoyaltyAccount.Create(UserId);

        account.GrantPoints(1600, LoyaltyEarnSource.OrderCompleted, "order-1", ActorId, configured);

        Assert.Equal(LoyaltyTier.GoldPolisher, account.CurrentTier);
    }

    [Fact]
    public void RecomputeTier_LegacyAndConfiguredDisagree_ConfiguredWins()
    {
        var configured = new LoyaltyTierThresholds(Silver: 300, Gold: 1500, Platinum: 4000);
        var account = LoyaltyAccount.Create(UserId);

        account.GrantPoints(1000, LoyaltyEarnSource.OrderCompleted, "order-1", ActorId, configured);

        Assert.Equal(LoyaltyTier.SilverMopper, account.CurrentTier);
    }

    [Fact]
    public void ResolveTier_BoundaryPointsLandOnTheConfiguredTier()
    {
        var thresholds = new LoyaltyTierThresholds(Silver: 300, Gold: 1500, Platinum: 4000);

        Assert.Equal(LoyaltyTier.BronzeCleaner, thresholds.ResolveTier(299));
        Assert.Equal(LoyaltyTier.SilverMopper, thresholds.ResolveTier(300));
        Assert.Equal(LoyaltyTier.GoldPolisher, thresholds.ResolveTier(1500));
        Assert.Equal(LoyaltyTier.PlatinumSparkler, thresholds.ResolveTier(4000));
    }

    [Fact]
    public void ResolveTier_MissingTierConfig_DegradesToUnreachableThreshold()
    {
        var thresholds = new LoyaltyTierThresholds(
            Silver: 300,
            Gold: int.MaxValue,
            Platinum: int.MaxValue);

        Assert.Equal(LoyaltyTier.SilverMopper, thresholds.ResolveTier(50_000));
    }

    [Fact]
    public void RecomputeTier_AllTierConfigMissing_DoesNotThrow_AndStaysLowestTier()
    {
        var allMissing = new LoyaltyTierThresholds(
            Silver: int.MaxValue,
            Gold: int.MaxValue,
            Platinum: int.MaxValue);
        var account = LoyaltyAccount.Create(UserId);

        var exception = Record.Exception(() =>
            account.GrantPoints(99_999, LoyaltyEarnSource.OrderCompleted, "order-1", ActorId, allMissing));

        Assert.Null(exception);
        Assert.Equal(LoyaltyTier.BronzeCleaner, account.CurrentTier);
    }
}
