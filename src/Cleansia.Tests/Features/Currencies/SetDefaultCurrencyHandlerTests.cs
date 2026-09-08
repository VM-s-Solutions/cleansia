using Cleansia.Core.AppServices.Features.Currencies;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Currencies;

/// <summary>
/// The platform default currency is settable. Single-default invariant: promoting a
/// currency clears the previous default in the SAME unit of work (one commit by the pipeline), so
/// exactly one default exists afterward. Mirrors <c>SetDefaultSavedAddress</c>'s clear-then-set.
/// Idempotent: re-promoting the current default succeeds without touching any other row.
/// </summary>
public class SetDefaultCurrencyHandlerTests
{
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();

    private SetDefaultCurrency.Handler CreateHandler() => new(_currencyRepository.Object);

    private Currency ArrangeCurrency(string id, string code, bool isDefault = false)
    {
        var currency = Currency.Create(code, code, code, 1.0m);
        currency.Id = id;
        currency.SetAsDefault(isDefault);
        _currencyRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        return currency;
    }

    /// <summary>
    /// <b>The default currency is the pricing currency.</b> Since the exchange rate left the pricing
    /// path, the calculator returns the catalogue's own numbers and labels them with the default
    /// currency's code — so promoting a currency the catalogue is not priced in charges CZK figures
    /// under that code. On the seeded basket that is roughly a 25x overcharge, on the card and on the
    /// fiscal receipt, from one star icon in Admin → Currencies.
    ///
    /// <para>This was introduced by the Wave A change that deleted the scaling, and <b>4410 passing
    /// tests did not see it</b> — the suite had no case where a non-default currency was promoted and
    /// then priced against. It was found by an adversarial review of the commit. Wave B replaces the
    /// <c>IsActive</c> condition with "has price rows in this currency".</para>
    /// </summary>
    [Fact]
    public async Task SetDefault_RefusesAnInactiveCurrency_BecauseTheCatalogueIsNotPricedInIt()
    {
        var previousDefault = ArrangeCurrency("currency-czk", "CZK", isDefault: true);
        var target = ArrangeCurrency("currency-eur", "EUR");
        target.IsActive = false;
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousDefault);

        var result = await CreateHandler().Handle(
            new SetDefaultCurrency.Command("currency-eur"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(target.IsDefault);
        Assert.True(previousDefault.IsDefault, "the existing default must survive a refused promotion");
    }

    [Fact]
    public async Task SetDefault_PromotesTarget_And_ClearsPreviousDefault()
    {
        var previousDefault = ArrangeCurrency("currency-czk", "CZK", isDefault: true);
        var target = ArrangeCurrency("currency-eur", "EUR");
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousDefault);

        var result = await CreateHandler().Handle(new SetDefaultCurrency.Command("currency-eur"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(target.IsDefault);
        Assert.False(previousDefault.IsDefault);
        Assert.Single(new[] { previousDefault, target }, c => c.IsDefault);
    }

    [Fact]
    public async Task SetDefault_OnCurrentDefault_IsIdempotent_TouchesNothingElse()
    {
        var current = ArrangeCurrency("currency-czk", "CZK", isDefault: true);

        var result = await CreateHandler().Handle(new SetDefaultCurrency.Command("currency-czk"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(current.IsDefault);
        _currencyRepository.Verify(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
