using Cleansia.Core.AppServices.Features.Currencies;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Cleansia.Tests.Features.Currencies;

/// <summary>
/// The platform default currency is settable, and exactly one default exists afterward.
///
/// <para>The invariant is now the DATABASE's — <c>IX_Currencies_IsDefault_Unique</c>, a partial unique
/// index Postgres cannot defer. That makes STATEMENT ORDER load-bearing rather than incidental: the
/// clear must be flushed before the promote is emitted, or the two-defaults instant is a 23505 and the
/// admin's star returns a 500. <see cref="SetDefault_FlushesTheClear_BeforeItPromotes"/> is what pins
/// that ordering here; the index itself is exercised against real Postgres in
/// <c>CurrencyUniqueIndexTests</c>, since SQLite ignores partial-index syntax entirely.</para>
///
/// <para>Idempotent: re-promoting the current default succeeds without touching any other row, and
/// without opening a transaction at all.</para>
/// </summary>
public class SetDefaultCurrencyHandlerTests
{
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();

    /// <summary>Flag snapshots, one per flush, in the order the handler flushed them.</summary>
    private readonly List<(bool PreviousIsDefault, bool TargetIsDefault)> _flushes = [];

    private SetDefaultCurrency.Handler CreateHandler() => new(_currencyRepository.Object);

    /// <summary>
    /// The handler wraps its two flushes in a transaction, so the repository double has to hand one
    /// back — a bare Moq returns null and the handler NREs on commit.
    /// </summary>
    private void ArrangeTransaction() =>
        _currencyRepository
            .Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDbContextTransaction>());

    /// <summary>
    /// The clear lives on the repository now, so the double has to perform it — otherwise the handler
    /// looks like it never cleared and the promote assertions below pass or fail for the wrong reason.
    /// </summary>
    private void ArrangeClear(params Currency[] currentDefaults) =>
        _currencyRepository
            .Setup(r => r.ClearDefaultAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Array.ForEach(currentDefaults, c => c.SetAsDefault(false)))
            .Returns(Task.CompletedTask);

    /// <summary>Records the two currencies' default flags at the moment of each flush.</summary>
    private void RecordFlushes(Currency previousDefault, Currency target) =>
        _currencyRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _flushes.Add((previousDefault.IsDefault, target.IsDefault)))
            .Returns(Task.CompletedTask);

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
        ArrangeClear(previousDefault);

        var result = await CreateHandler().Handle(
            new SetDefaultCurrency.Command("currency-eur"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(target.IsDefault);
        Assert.True(previousDefault.IsDefault, "the existing default must survive a refused promotion");
    }

    /// <summary>
    /// THE ORDERING, which is what the partial unique index requires and what an unflushed
    /// clear-then-set does not guarantee. Two flushes: the first must see the previous default already
    /// cleared and the target NOT yet promoted — the only arrangement in which no statement ever leaves
    /// two rows true. A handler that promoted first would record (true, true) here, which is precisely
    /// the 23505 Postgres raises.
    /// </summary>
    [Fact]
    public async Task SetDefault_FlushesTheClear_BeforeItPromotes()
    {
        var previousDefault = ArrangeCurrency("currency-czk", "CZK", isDefault: true);
        var target = ArrangeCurrency("currency-eur", "EUR");
        ArrangeClear(previousDefault);
        ArrangeTransaction();
        RecordFlushes(previousDefault, target);

        var result = await CreateHandler().Handle(
            new SetDefaultCurrency.Command("currency-eur"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([(false, false), (false, true)], _flushes);
    }

    [Fact]
    public async Task SetDefault_PromotesTarget_And_ClearsPreviousDefault()
    {
        var previousDefault = ArrangeCurrency("currency-czk", "CZK", isDefault: true);
        var target = ArrangeCurrency("currency-eur", "EUR");
        ArrangeClear(previousDefault);
        ArrangeTransaction();

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

        // Nothing is written at all -- no clear, no flush, no transaction. Verified against the CLEAR
        // rather than against GetDefaultAsync, which the handler no longer calls on any path and which
        // would therefore be a Times.Never that can never fail.
        _currencyRepository.Verify(r => r.ClearDefaultAsync(It.IsAny<CancellationToken>()), Times.Never);
        _currencyRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _currencyRepository.Verify(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
