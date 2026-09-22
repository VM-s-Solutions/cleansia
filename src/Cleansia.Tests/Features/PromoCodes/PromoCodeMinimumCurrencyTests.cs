using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.PromoCodes;

/// <summary>
/// A promo code's minimum is a number in ONE currency: the code's own when it names one (a fixed
/// discount always does), else the platform default -- which is how every percent code with a minimum
/// was authored (the seed says "1500 CZK" beside a null currency). So a code with a minimum is bound to
/// that currency and refused as a currency mismatch on an order in any other, BEFORE the minimum is
/// compared: comparing 1500 CZK against a EUR subtotal answers nothing, and the customer is told which
/// rule refused. A percent code with no minimum carries no money and stays global (T-0703).
/// </summary>
public class PromoCodeMinimumCurrencyTests
{
    private const string UserId = "user-1";
    private const string OrderId = "order-1";
    private const string Czk = "czk";
    private const string Eur = "eur";

    private readonly Mock<IPromoCodeRepository> _promoCodes = new();
    private readonly Mock<IPromoCodeRedemptionRepository> _redemptions = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();

    public PromoCodeMinimumCurrencyTests()
    {
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = Czk;
        czk.SetAsDefault(true);
        _currencyRepository.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(czk);

        _redemptions
            .Setup(r => r.CountForUserAndCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _redemptions
            .Setup(r => r.GetByOrderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoCodeRedemption?)null);
    }

    [Fact]
    public async Task Percent_Code_With_A_Minimum_Is_Bound_To_The_Default_Currency()
    {
        ArrangeCode(PromoCode.CreatePercent("SPRING20", percent: 0.20m, minimumOrderAmount: 1500m));

        var onEur = await CreateService().PreviewAsync("SPRING20", UserId, 2000m, Eur, CancellationToken.None);
        var onCzk = await CreateService().PreviewAsync("SPRING20", UserId, 2000m, Czk, CancellationToken.None);

        Assert.False(onEur.Success);
        Assert.Equal(PromoCodeError.CurrencyMismatch, onEur.Error);
        Assert.True(onCzk.Success);
        Assert.Equal(400m, onCzk.DiscountAmount);
    }

    [Fact]
    public async Task Percent_Code_Without_A_Minimum_Is_Global()
    {
        ArrangeCode(PromoCode.CreatePercent("WELCOME15", percent: 0.15m, minimumOrderAmount: null));

        var result = await CreateService().PreviewAsync("WELCOME15", UserId, 100m, Eur, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(15m, result.DiscountAmount);
        _currencyRepository.Verify(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Fixed_Code_Reports_CurrencyMismatch_Before_BelowMinimum()
    {
        ArrangeCode(PromoCode.CreateFixed("OFF200", amount: 200m, currencyId: Czk, minimumOrderAmount: 1000m));

        var preview = await CreateService().PreviewAsync("OFF200", UserId, 50m, Eur, CancellationToken.None);
        var apply = await CreateService().ApplyAsync("OFF200", UserId, OrderId, 50m, Eur, CancellationToken.None);

        Assert.Equal(PromoCodeError.CurrencyMismatch, preview.Error);
        Assert.Equal(PromoCodeError.CurrencyMismatch, apply.Error);
        // A code that names its currency needs no default lookup.
        _currencyRepository.Verify(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Default_Currency_Order_Below_The_Minimum_Is_Still_Refused()
    {
        ArrangeCode(PromoCode.CreatePercent("SPRING20", percent: 0.20m, minimumOrderAmount: 1500m));

        var result = await CreateService().PreviewAsync("SPRING20", UserId, 1000m, Czk, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PromoCodeError.BelowMinimumOrderAmount, result.Error);
    }

    // ── arrangement ──────────────────────────────────────────────────

    private PromoCodeService CreateService() =>
        new(_promoCodes.Object, _redemptions.Object, _currencyRepository.Object, NullLogger<PromoCodeService>.Instance);

    private void ArrangeCode(PromoCode code)
    {
        code.Id = "promo-1";
        _promoCodes
            .Setup(r => r.GetByCodeAsync(code.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
    }
}
