using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// The tier discount's floor (<c>LoyaltyTierConfig.MinimumOrderAmountForDiscount</c>) is a number in
/// the PLATFORM DEFAULT currency and is enforced only on an order in that currency. Compared verbatim,
/// "1000" meant a EUR order needed 1000 EUR before Silver's advertised 5% was granted -- a whole market
/// silently denied the promise. On any other currency no floor applies, and the floor that was JUDGED
/// comes back so the quote states the same rule the order will use (T-0703).
/// </summary>
public class TierDiscountFloorCurrencyTests
{
    private const string UserId = "user-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ILoyaltyAccountRepository> _accountRepository = new();
    private readonly Mock<ILoyaltyTierConfigRepository> _tierConfigRepository = new();
    private readonly Mock<ILoyaltyTransactionRepository> _transactionRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<INotificationProducer> _producer = new();

    public TierDiscountFloorCurrencyTests()
    {
        var account = LoyaltyAccount.Create(UserId);
        account.GrantPoints(600, LoyaltyEarnSource.OrderCompleted, "order-0", "system", new LoyaltyTierThresholds(500, 2000, 5000));
        _accountRepository
            .Setup(r => r.GetByUserIdTierOnlyAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        Assert.Equal(LoyaltyTier.SilverMopper, account.CurrentTier);

        ArrangeCurrency("czk", isDefault: true);
        ArrangeCurrency("eur", isDefault: false);
    }

    [Fact]
    public async Task Floor_Is_Enforced_On_A_Default_Currency_Order()
    {
        ArrangeSilver(discountPercent: 0.05m, floor: 1000m);

        var result = await CreateService().ResolveTierDiscountForOrderAsync(UserId, 900m, "czk", CancellationToken.None);

        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(LoyaltyTier.SilverMopper, result.TierAtPurchase);
        Assert.Equal(1000m, result.MinimumOrderAmount);
    }

    [Fact]
    public async Task Floor_Is_Not_Applied_On_A_NonDefault_Currency_Order()
    {
        ArrangeSilver(discountPercent: 0.05m, floor: 1000m);

        var result = await CreateService().ResolveTierDiscountForOrderAsync(UserId, 30m, "eur", CancellationToken.None);

        Assert.Equal(1.50m, result.DiscountAmount);
        Assert.Equal(LoyaltyTier.SilverMopper, result.TierAtPurchase);
        Assert.Null(result.MinimumOrderAmount);
    }

    [Fact]
    public async Task A_Default_Currency_Order_At_Or_Above_The_Floor_Is_Discounted_And_States_It()
    {
        ArrangeSilver(discountPercent: 0.05m, floor: 1000m);

        var result = await CreateService().ResolveTierDiscountForOrderAsync(UserId, 1000m, "czk", CancellationToken.None);

        Assert.Equal(50m, result.DiscountAmount);
        Assert.Equal(1000m, result.MinimumOrderAmount);
    }

    [Fact]
    public async Task No_Configured_Floor_Reports_Null_And_Never_Reads_The_Currency()
    {
        ArrangeSilver(discountPercent: 0.05m, floor: null);

        var result = await CreateService().ResolveTierDiscountForOrderAsync(UserId, 30m, "eur", CancellationToken.None);

        Assert.Equal(1.50m, result.DiscountAmount);
        Assert.Null(result.MinimumOrderAmount);
        _currencyRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── arrangement ──────────────────────────────────────────────────

    private LoyaltyService CreateService() =>
        new(
            _orderRepository.Object,
            _accountRepository.Object,
            _tierConfigRepository.Object,
            _transactionRepository.Object,
            _currencyRepository.Object,
            _producer.Object,
            NullLogger<LoyaltyService>.Instance);

    private void ArrangeSilver(decimal discountPercent, decimal? floor) =>
        _tierConfigRepository
            .Setup(r => r.GetByTierAsync(LoyaltyTier.SilverMopper, It.IsAny<CancellationToken>()))
            .ReturnsAsync(LoyaltyTierConfig.Create(LoyaltyTier.SilverMopper, 500, discountPercent, floor, "[]"));

    private void ArrangeCurrency(string id, bool isDefault)
    {
        var currency = Currency.Create(id.ToUpperInvariant(), "¤", id);
        currency.Id = id;
        currency.SetAsDefault(isDefault);
        _currencyRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
    }
}
