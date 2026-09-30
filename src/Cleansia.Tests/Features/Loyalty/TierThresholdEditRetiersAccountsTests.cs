using Cleansia.Core.AppServices.Features.Loyalty.Admin;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// The tier follows points both ways (owner ruling 2026-09-28), and the discount reads the stored tier.
/// A threshold edit that re-tiered nobody left every customer on their old tier and discount until their
/// next points movement, while the admin preview had just reported how many would move. The save now
/// moves them.
/// </summary>
public sealed class TierThresholdEditRetiersAccountsTests
{
    private const string Admin = "admin-tiers";

    private readonly LoyaltyTierConfig _bronze = Config(LoyaltyTier.BronzeCleaner, 0);
    private readonly LoyaltyTierConfig _silver = Config(LoyaltyTier.SilverMopper, 500);
    private readonly LoyaltyTierConfig _gold = Config(LoyaltyTier.GoldPolisher, 1500);
    private readonly LoyaltyTierConfig _platinum = Config(LoyaltyTier.PlatinumSparkler, 3000);

    private readonly Mock<ILoyaltyTierConfigRepository> _configs = new();
    private readonly Mock<ILoyaltyAccountRepository> _accounts = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public TierThresholdEditRetiersAccountsTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(Admin);
        _configs.Setup(r => r.GetByIdAsync(_silver.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_silver);
        _configs.Setup(r => r.GetAllForTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([_bronze, _silver, _gold, _platinum]);
    }

    [Fact]
    public async Task Raising_A_Threshold_Moves_An_Account_That_No_Longer_Reaches_It_Down()
    {
        var silver = AccountWith(600);
        var gold = AccountWith(2000);
        ArrangeAccounts(silver, gold);

        await SaveSilverThresholdAsync(700);

        Assert.Equal(LoyaltyTier.BronzeCleaner, silver.CurrentTier);
        Assert.Equal(LoyaltyTier.GoldPolisher, gold.CurrentTier);
    }

    [Fact]
    public async Task Lowering_A_Threshold_Moves_An_Account_That_Now_Reaches_It_Up()
    {
        var bronze = AccountWith(450);
        ArrangeAccounts(bronze);

        await SaveSilverThresholdAsync(400);

        Assert.Equal(LoyaltyTier.SilverMopper, bronze.CurrentTier);
    }

    [Fact]
    public async Task An_Edit_That_Moves_No_Threshold_Past_An_Account_Leaves_It_Alone()
    {
        var silver = AccountWith(600);
        var achievedOn = silver.TierAchievedOn;
        ArrangeAccounts(silver);

        await SaveSilverThresholdAsync(550);

        Assert.Equal(LoyaltyTier.SilverMopper, silver.CurrentTier);
        Assert.Equal(achievedOn, silver.TierAchievedOn);
    }

    private async Task SaveSilverThresholdAsync(int threshold)
    {
        var result = await new UpdateTierConfig.Handler(_configs.Object, _accounts.Object, _session.Object)
            .Handle(new UpdateTierConfig.Command(_silver.Id, threshold, 0.05m, null, "{}"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    private void ArrangeAccounts(params LoyaltyAccount[] accounts) =>
        _accounts.Setup(r => r.GetQueryableIgnoringTenant()).Returns(accounts.AsQueryable().BuildMock());

    private LoyaltyAccount AccountWith(int points)
    {
        var account = LoyaltyAccount.Create($"user-{points}");
        account.GrantPoints(
            points,
            LoyaltyEarnSource.ManualGrant,
            orderId: null,
            actorId: Admin,
            new LoyaltyTierThresholds(_silver.LifetimePointsThreshold, _gold.LifetimePointsThreshold, _platinum.LifetimePointsThreshold));
        return account;
    }

    private static LoyaltyTierConfig Config(LoyaltyTier tier, int threshold) =>
        LoyaltyTierConfig.Create(tier, threshold, 0.05m, null, "{}");
}
