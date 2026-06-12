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
