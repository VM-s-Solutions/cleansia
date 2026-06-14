using Cleansia.Core.AppServices.Features.Loyalty.Admin;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// LG-09 — the tier-threshold preview is a pure read (only AsNoTracking reads). Converting it from
/// the command pipeline to a query must not change the computed <c>Impacts</c>. Pins the exact
/// current/new tier counts and deltas for a fixed account set so the CQRS reclassification is proven
/// behavior-identical.
/// </summary>
public class PreviewTierThresholdImpactHandlerTests
{
    private readonly Mock<ILoyaltyAccountRepository> _accountRepository = new();
    private readonly Mock<ILoyaltyTierConfigRepository> _tierConfigRepository = new();

    private static LoyaltyAccount AccountWithLifetimePoints(string id, int points)
    {
        var account = LoyaltyAccount.Create("u-" + id);
        account.Id = id;
        if (points > 0)
        {
            account.GrantPoints(points, LoyaltyEarnSource.OrderCompleted, "order-" + id, "system",
                new LoyaltyTierThresholds(Silver: int.MaxValue, Gold: int.MaxValue, Platinum: int.MaxValue));
        }
        return account;
    }

    private void ArrangeCurrentThresholds()
    {
        var configs = new[]
        {
            LoyaltyTierConfig.Create(LoyaltyTier.BronzeCleaner, 0, 0m, null, "[]"),
            LoyaltyTierConfig.Create(LoyaltyTier.SilverMopper, 500, 0m, null, "[]"),
            LoyaltyTierConfig.Create(LoyaltyTier.GoldPolisher, 2000, 0m, null, "[]"),
            LoyaltyTierConfig.Create(LoyaltyTier.PlatinumSparkler, 5000, 0m, null, "[]"),
        };
        _tierConfigRepository
            .Setup(r => r.GetAllForTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);
    }

    private void ArrangeAccounts(params int[] lifetimePoints)
    {
        var accounts = lifetimePoints
            .Select((p, i) => AccountWithLifetimePoints("acct-" + i, p))
            .ToList();
        _accountRepository.Setup(r => r.GetQueryable()).Returns(accounts.AsQueryable().BuildMock());
    }

    private PreviewTierThresholdImpact.Handler CreateHandler() =>
        new(_accountRepository.Object, _tierConfigRepository.Object);

    [Fact]
    public async Task Preview_ComputesCurrentVsProposed_TierCountsAndDeltas()
    {
        ArrangeCurrentThresholds();
        // 100→Bronze, 600→Silver, 2500→Gold, 6000→Platinum under the current thresholds.
        ArrangeAccounts(100, 600, 2500, 6000);

        // Proposed: lower Silver to 50 so the 100-point account moves Bronze→Silver.
        var result = await CreateHandler().Handle(
            new PreviewTierThresholdImpact.Query(
                BronzeThreshold: 0, SilverThreshold: 50, GoldThreshold: 2000, PlatinumThreshold: 5000),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var impacts = result.Value.Impacts.ToDictionary(i => i.Tier);

        Assert.Equal(1, impacts[LoyaltyTier.BronzeCleaner].CurrentCount);
        Assert.Equal(0, impacts[LoyaltyTier.BronzeCleaner].NewCount);
        Assert.Equal(-1, impacts[LoyaltyTier.BronzeCleaner].Delta);

        Assert.Equal(1, impacts[LoyaltyTier.SilverMopper].CurrentCount);
        Assert.Equal(2, impacts[LoyaltyTier.SilverMopper].NewCount);
        Assert.Equal(1, impacts[LoyaltyTier.SilverMopper].Delta);

        Assert.Equal(1, impacts[LoyaltyTier.GoldPolisher].CurrentCount);
        Assert.Equal(1, impacts[LoyaltyTier.GoldPolisher].NewCount);
        Assert.Equal(0, impacts[LoyaltyTier.GoldPolisher].Delta);

        Assert.Equal(1, impacts[LoyaltyTier.PlatinumSparkler].CurrentCount);
        Assert.Equal(1, impacts[LoyaltyTier.PlatinumSparkler].NewCount);
        Assert.Equal(0, impacts[LoyaltyTier.PlatinumSparkler].Delta);
    }
}
