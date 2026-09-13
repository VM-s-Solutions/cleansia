using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Countries;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Cleansia.Tests.Features.Countries;

/// <summary>
/// One configuration carries the default-market flag afterwards, and the invariant is the
/// database's (<c>IX_CountryConfigurations_IsDefaultMarket_Unique</c>, a partial unique index Postgres
/// cannot defer) — so the clear must be flushed before the promote is emitted, exactly as
/// <c>SetDefaultCurrency</c> does it. Idempotent on the current default.
/// </summary>
public class SetDefaultMarketHandlerTests
{
    private readonly Mock<ICountryConfigurationRepository> _configurations = new();

    private readonly List<(bool PreviousFlag, bool TargetFlag)> _flushes = [];

    private SetDefaultMarket.Handler CreateHandler() => new(_configurations.Object);

    private CountryConfiguration ArrangeConfiguration(string countryId, bool isDefaultMarket = false)
    {
        var configuration = CountryConfiguration.Create(countryId, "CZK", "cs", 0.21m);
        configuration.SetAsDefaultMarket(isDefaultMarket);
        _configurations
            .Setup(r => r.GetByCountryIdAsync(countryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
        return configuration;
    }

    private void ArrangeClear(params CountryConfiguration[] currentDefaults) =>
        _configurations
            .Setup(r => r.ClearDefaultMarketAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Array.ForEach(currentDefaults, c => c.SetAsDefaultMarket(false)))
            .Returns(Task.CompletedTask);

    private void ArrangeTransaction() =>
        _configurations
            .Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IDbContextTransaction>());

    private void RecordFlushes(CountryConfiguration previous, CountryConfiguration target) =>
        _configurations
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _flushes.Add((previous.IsDefaultMarket, target.IsDefaultMarket)))
            .Returns(Task.CompletedTask);

    [Fact]
    public async Task Promotes_The_Target_And_Clears_The_Previous_Default_So_Exactly_One_Is_Flagged()
    {
        var previous = ArrangeConfiguration("country-cze", isDefaultMarket: true);
        var target = ArrangeConfiguration("country-svk");
        ArrangeClear(previous);
        ArrangeTransaction();

        var result = await CreateHandler().Handle(new SetDefaultMarket.Command("country-svk"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("country-svk", result.Value.CountryId);
        Assert.True(target.IsDefaultMarket);
        Assert.False(previous.IsDefaultMarket);
        Assert.Single(new[] { previous, target }, c => c.IsDefaultMarket);
    }

    /// <summary>
    /// The first flush must see the previous default already cleared and the target NOT yet
    /// promoted; a handler that promoted first would record (true, true), which is the 23505.
    /// </summary>
    [Fact]
    public async Task Flushes_The_Clear_Before_It_Promotes()
    {
        var previous = ArrangeConfiguration("country-cze", isDefaultMarket: true);
        var target = ArrangeConfiguration("country-svk");
        ArrangeClear(previous);
        ArrangeTransaction();
        RecordFlushes(previous, target);

        var result = await CreateHandler().Handle(new SetDefaultMarket.Command("country-svk"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([(false, false), (false, true)], _flushes);
    }

    [Fact]
    public async Task On_The_Current_Default_It_Is_Idempotent_And_Writes_Nothing()
    {
        var current = ArrangeConfiguration("country-cze", isDefaultMarket: true);

        var result = await CreateHandler().Handle(new SetDefaultMarket.Command("country-cze"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(current.IsDefaultMarket);
        _configurations.Verify(r => r.ClearDefaultMarketAsync(It.IsAny<CancellationToken>()), Times.Never);
        _configurations.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _configurations.Verify(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The validator refuses a country with no configuration; the handler's own guard names the same key.</summary>
    [Fact]
    public async Task A_Country_With_No_Configuration_Is_Not_Ready()
    {
        _configurations
            .Setup(r => r.GetByCountryIdAsync("country-none", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CountryConfiguration?)null);

        var result = await CreateHandler().Handle(new SetDefaultMarket.Command("country-none"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.CountryMarketNotReady, result.Error!.Message);
    }
}
