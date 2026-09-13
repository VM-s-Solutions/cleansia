using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Countries;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Countries;

/// <summary>
/// The default market is a pre-selection of a LISTED market (owner ruling 2026-09-13, Q-MARKET-01),
/// so the gate is the servicing gate plus serviced itself: a flag on a country that
/// <c>Market/GetOverview</c> would not list is a pre-selection of nothing.
/// </summary>
public class SetDefaultMarketValidatorTests
{
    private const string CountryId = "country-1";

    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<ICountryConfigurationRepository> _configurations = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();

    public SetDefaultMarketValidatorTests()
    {
        _countries.Setup(r => r.ExistsAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countries.Setup(r => r.IsServicedAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _configurations.Setup(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CountryConfiguration?)null);
        _currencies.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Currency?)null);
    }

    private SetDefaultMarket.Validator Validator() =>
        new(_countries.Object, _configurations.Object, _currencies.Object);

    private void ArrangeConfiguration(string currencyCode, bool? currencyIsActive)
    {
        _configurations.Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(CountryId, currencyCode, "en", 0.2m));

        if (currencyIsActive is { } active)
        {
            var currency = Currency.Create(currencyCode, currencyCode, currencyCode);
            currency.IsActive = active;
            _currencies.Setup(r => r.GetByCodeAsync(currencyCode, It.IsAny<CancellationToken>())).ReturnsAsync(currency);
        }
    }

    [Fact]
    public async Task An_Unknown_Country_Is_Not_Found()
    {
        var result = await Validator().ValidateAsync(new SetDefaultMarket.Command("country-missing"));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CountryNotFound, error.ErrorMessage);
        Assert.Equal(nameof(SetDefaultMarket.Command.CountryId), error.PropertyName);
    }

    [Fact]
    public async Task An_Unserviced_Country_Is_Refused_Before_The_Market_Gate()
    {
        ArrangeConfiguration("CZK", currencyIsActive: true);
        _countries.Setup(r => r.IsServicedAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await Validator().ValidateAsync(new SetDefaultMarket.Command(CountryId));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.CountryNotServiced, Assert.Single(result.Errors).ErrorMessage);
        _configurations.Verify(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Country_With_No_Configuration_Is_Not_Ready()
    {
        var result = await Validator().ValidateAsync(new SetDefaultMarket.Command(CountryId));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.CountryMarketNotReady, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Configured_Code_Naming_No_Currency_Is_Not_Ready()
    {
        ArrangeConfiguration("XXX", currencyIsActive: null);

        var result = await Validator().ValidateAsync(new SetDefaultMarket.Command(CountryId));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.CountryMarketNotReady, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Country_Whose_Currency_Is_Inactive_Is_Not_Ready()
    {
        ArrangeConfiguration("EUR", currencyIsActive: false);

        var result = await Validator().ValidateAsync(new SetDefaultMarket.Command(CountryId));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.CountryMarketNotReady, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Serviced_Country_On_An_Active_Configured_Currency_Passes()
    {
        ArrangeConfiguration("CZK", currencyIsActive: true);

        var result = await Validator().ValidateAsync(new SetDefaultMarket.Command(CountryId));

        Assert.True(result.IsValid);
    }
}
