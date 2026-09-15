using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Countries;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Countries;

/// <summary>
/// ADR-0058 D7 gate 2. <c>Country/GetServiced</c> feeds the wizard's address step, so a serviced
/// country the quote cannot price is a customer-visible dead end: switching a country ON needs a
/// configuration whose default currency is switched on. Switching OFF is never gated.
/// </summary>
public class SetCountryServicedValidatorTests
{
    private const string CountryId = "country-1";

    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<ICountryConfigurationRepository> _configurations = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();

    public SetCountryServicedValidatorTests()
    {
        _countries.Setup(r => r.ExistsAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _configurations.Setup(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CountryConfiguration?)null);
        _currencies.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Currency?)null);
    }

    private SetCountryServiced.Validator Validator() =>
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
    public async Task Servicing_A_Country_With_No_Configuration_Is_Refused()
    {
        var result = await Validator().ValidateAsync(new SetCountryServiced.Command(CountryId, IsServiced: true));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CountryMarketNotReady, error.ErrorMessage);
        Assert.Equal(nameof(SetCountryServiced.Command.CountryId), error.PropertyName);
    }

    [Fact]
    public async Task Servicing_A_Country_Whose_Currency_Is_Inactive_Is_Refused()
    {
        ArrangeConfiguration("EUR", currencyIsActive: false);

        var result = await Validator().ValidateAsync(new SetCountryServiced.Command(CountryId, IsServiced: true));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.CountryMarketNotReady, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Servicing_A_Country_Whose_Configured_Code_Names_No_Currency_Is_Refused()
    {
        ArrangeConfiguration("XXX", currencyIsActive: null);

        var result = await Validator().ValidateAsync(new SetCountryServiced.Command(CountryId, IsServiced: true));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.CountryMarketNotReady, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Servicing_A_Country_Whose_Currency_Is_Active_Passes()
    {
        ArrangeConfiguration("CZK", currencyIsActive: true);

        var result = await Validator().ValidateAsync(new SetCountryServiced.Command(CountryId, IsServiced: true));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Unservicing_Is_Never_Gated()
    {
        var result = await Validator().ValidateAsync(new SetCountryServiced.Command(CountryId, IsServiced: false));

        Assert.True(result.IsValid);
        _configurations.Verify(
            r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A missing country is reported as missing, not as an unready market.</summary>
    [Fact]
    public async Task An_Unknown_Country_Is_Not_Found_Before_The_Gate_Runs()
    {
        var result = await Validator().ValidateAsync(new SetCountryServiced.Command("country-missing", IsServiced: true));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.CountryNotFound, Assert.Single(result.Errors).ErrorMessage);
    }
}
