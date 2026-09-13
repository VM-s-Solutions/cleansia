using Cleansia.Core.AppServices.Features.Markets;
using Cleansia.Core.AppServices.Features.Markets.DTOs;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Tenancy;

/// <summary>
/// ADR-0061 D3 — two answers for one country id. Is it a market (ADR-0058 D1's three predicates), and
/// which operating company serves it. Null names the default market, the SAME one the directory flags.
/// </summary>
public sealed class OperatorTenantResolverTests
{
    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<ICountryConfigurationRepository> _configurations = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<IRequestHandler<GetMarkets.Request, IReadOnlyList<MarketListItem>>> _directory = new();

    public OperatorTenantResolverTests()
    {
        _countries.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Country?)null);
        _configurations.Setup(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CountryConfiguration?)null);
        _currencies.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Currency?)null);
        _directory.Setup(h => h.Handle(It.IsAny<GetMarkets.Request>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private OperatorTenantResolver Resolver() =>
        new(_countries.Object, _configurations.Object, _currencies.Object, _directory.Object, NullLogger<OperatorTenantResolver>.Instance);

    private void ServicedCountry(string id, bool serviced = true, bool active = true)
    {
        var country = Country.Create(id, id, id[..2], serviced);
        country.Id = id;
        country.IsActive = active;
        _countries.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(country);
    }

    private void Configuration(string countryId, string currencyCode, string? operatorTenantId)
    {
        var configuration = CountryConfiguration.Create(countryId, currencyCode, "en", 0.2m).AssignOperator(operatorTenantId);
        _configurations.Setup(r => r.GetByCountryIdAsync(countryId, It.IsAny<CancellationToken>())).ReturnsAsync(configuration);
    }

    private void Currency(string code, bool active = true)
    {
        var currency = Cleansia.Core.Domain.Internationalization.Currency.Create(code, code, code);
        currency.IsActive = active;
        _currencies.Setup(r => r.GetByCodeAsync(code, It.IsAny<CancellationToken>())).ReturnsAsync(currency);
    }

    [Fact]
    public async Task A_Serviced_Configured_Currency_Active_Country_Is_A_Market_With_Its_Operator()
    {
        ServicedCountry("SVK");
        Configuration("SVK", "EUR", "cleansia-sk");
        Currency("EUR");

        Assert.Equal(new OperatorResolution(true, "cleansia-sk"), await Resolver().ResolveAsync("SVK", CancellationToken.None));
    }

    [Fact]
    public async Task A_Market_Nobody_Operates_Is_A_Market_With_No_Operator()
    {
        ServicedCountry("POL");
        Configuration("POL", "PLN", operatorTenantId: null);
        Currency("PLN");

        Assert.Equal(new OperatorResolution(true, null), await Resolver().ResolveAsync("POL", CancellationToken.None));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("unserviced")]
    [InlineData("inactive")]
    [InlineData("unconfigured")]
    [InlineData("currency-off")]
    public async Task A_Country_Failing_Any_Market_Predicate_Is_Not_A_Market(string shape)
    {
        switch (shape)
        {
            case "unserviced":
                ServicedCountry("DEU", serviced: false);
                Configuration("DEU", "EUR", "cleansia-cz");
                Currency("EUR");
                break;
            case "inactive":
                ServicedCountry("DEU", active: false);
                Configuration("DEU", "EUR", "cleansia-cz");
                Currency("EUR");
                break;
            case "unconfigured":
                ServicedCountry("DEU");
                Currency("EUR");
                break;
            case "currency-off":
                ServicedCountry("DEU");
                Configuration("DEU", "EUR", "cleansia-cz");
                Currency("EUR", active: false);
                break;
        }

        Assert.Equal(OperatorResolution.NotAMarket, await Resolver().ResolveAsync("DEU", CancellationToken.None));
    }

    [Fact]
    public async Task No_Country_Means_The_Directorys_Default_Market_And_Its_Operator()
    {
        _directory.Setup(h => h.Handle(It.IsAny<GetMarkets.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Listed("SVK", isDefault: false), Listed("CZE", isDefault: true)]);
        Configuration("CZE", "CZK", "cleansia-cz");
        Configuration("SVK", "EUR", "cleansia-sk");

        Assert.Equal(new OperatorResolution(true, "cleansia-cz"), await Resolver().ResolveAsync(null, CancellationToken.None));
        _countries.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task No_Default_Market_At_All_Is_A_Market_Without_An_Operator_Not_An_Unserviced_Country()
    {
        _directory.Setup(h => h.Handle(It.IsAny<GetMarkets.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Listed("SVK", isDefault: false)]);

        Assert.Equal(new OperatorResolution(true, null), await Resolver().ResolveAsync(null, CancellationToken.None));
    }

    private static MarketListItem Listed(string iso, bool isDefault) => new(
        CountryId: iso, IsoCode: iso, IsoAlpha2: iso[..2], Name: iso, Translations: new Dictionary<string, Translation>(),
        CurrencyId: "cur", CurrencyCode: "CZK", CurrencySymbol: "Kč", IsDefault: isDefault, NoShowCredit: null, InsuranceCoverageAmount: null);
}
