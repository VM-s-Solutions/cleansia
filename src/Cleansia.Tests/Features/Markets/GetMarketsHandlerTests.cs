using Cleansia.Core.AppServices.Features.Markets;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Features.Markets;

/// <summary>
/// The market directory (ADR-0058 D1–D2). A market is a serviced country joined to its configured,
/// ACTIVE currency; the default market is the one on the platform default currency. This is the
/// anonymous read behind the landing page, so every configuration state below answers with a list
/// and a log line — never a throw.
/// </summary>
public class GetMarketsHandlerTests
{
    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<ICountryConfigurationRepository> _configurations = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly List<(LogLevel Level, string Message)> _log = [];

    private readonly List<Country> _serviced = [];

    public GetMarketsHandlerTests()
    {
        _countries.Setup(r => r.GetServicedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _serviced.OrderBy(c => c.Name).ToList());
        _configurations.Setup(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CountryConfiguration?)null);
        _currencies.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Currency?)null);
        _currencies.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("Default Currency was not found"));
    }

    private Currency Currency(string code, bool isActive = true, bool isDefault = false, decimal? noShowCredit = null)
    {
        var currency = Cleansia.Core.Domain.Internationalization.Currency.Create(code, code, code);
        currency.Id = $"cur-{code}";
        currency.IsActive = isActive;
        currency.SetAsDefault(isDefault);
        currency.SetNoShowCredit(noShowCredit);
        _currencies.Setup(r => r.GetByCodeAsync(code, It.IsAny<CancellationToken>())).ReturnsAsync(currency);
        if (isDefault)
        {
            _currencies.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(currency);
        }

        return currency;
    }

    private Country Market(string name, string iso3, string iso2, string? currencyCode, decimal? insurance = null)
    {
        var country = Country.Create(name, iso3, iso2, isServiced: true);
        country.Id = $"country-{iso3}";
        _serviced.Add(country);

        if (currencyCode is not null)
        {
            var configuration = CountryConfiguration.Create(country.Id, currencyCode, "en", 0.2m);
            configuration.UpdateMarketContent(insurance);
            _configurations.Setup(r => r.GetByCountryIdAsync(country.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(configuration);
        }

        return country;
    }

    private Task<IReadOnlyList<Cleansia.Core.AppServices.Features.Markets.DTOs.MarketListItem>> Run() =>
        new GetMarkets.Handler(_countries.Object, _configurations.Object, _currencies.Object, new CapturingLogger(_log))
            .Handle(new GetMarkets.Request(), default);

    [Fact]
    public async Task A_Serviced_Country_On_An_Active_Currency_Is_A_Market_With_Its_Figures()
    {
        Currency("CZK", isDefault: true, noShowCredit: 250m);
        Market("Czechia", "CZE", "CZ", "CZK", insurance: 1_000_000m);

        var markets = await Run();

        var czech = Assert.Single(markets);
        Assert.Equal("country-CZE", czech.CountryId);
        Assert.Equal("CZE", czech.IsoCode);
        Assert.Equal("CZ", czech.IsoAlpha2);
        Assert.Equal("cur-CZK", czech.CurrencyId);
        Assert.Equal("CZK", czech.CurrencyCode);
        Assert.True(czech.IsDefault);
        Assert.Equal(250m, czech.NoShowCredit);
        Assert.Equal(1_000_000m, czech.InsuranceCoverageAmount);
        Assert.DoesNotContain(_log, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task A_Serviced_Country_With_No_Configuration_Is_Omitted_And_Warned_About()
    {
        Currency("CZK", isDefault: true);
        Market("Czechia", "CZE", "CZ", "CZK");
        Market("Slovakia", "SVK", "SK", currencyCode: null);

        var markets = await Run();

        Assert.Equal(["CZE"], markets.Select(m => m.IsoCode));
        var warning = Assert.Single(_log, e => e.Level == LogLevel.Warning);
        Assert.Contains("SVK", warning.Message);
    }

    [Fact]
    public async Task A_Serviced_Country_On_An_Inactive_Currency_Is_Omitted_And_Warned_About()
    {
        Currency("CZK", isDefault: true);
        Currency("EUR", isActive: false);
        Market("Czechia", "CZE", "CZ", "CZK");
        Market("Slovakia", "SVK", "SK", "EUR");

        var markets = await Run();

        Assert.Equal(["CZE"], markets.Select(m => m.IsoCode));
        var warning = Assert.Single(_log, e => e.Level == LogLevel.Warning);
        Assert.Contains("SVK", warning.Message);
        Assert.Contains("EUR", warning.Message);
    }

    [Fact]
    public async Task A_Configured_Code_Naming_No_Currency_Is_Omitted_And_Warned_About()
    {
        Currency("CZK", isDefault: true);
        Market("Czechia", "CZE", "CZ", "CZK");
        Market("Atlantis", "ATL", "AT", "XXX");

        var markets = await Run();

        Assert.Equal(["CZE"], markets.Select(m => m.IsoCode));
        var warning = Assert.Single(_log, e => e.Level == LogLevel.Warning);
        Assert.Contains("ATL", warning.Message);
    }

    /// <summary>
    /// Several markets on the default currency is the ordinary state once EUR is the default and two
    /// EUR countries are serviced. Exactly one is pre-selected, by lowest ISO code, and the error log
    /// names the currency and every candidate — the owner picks explicitly from there (Q-MARKET-01).
    /// </summary>
    [Fact]
    public async Task Several_Markets_On_The_Default_Currency_Flag_The_Lowest_Iso_Code_And_Log_An_Error()
    {
        Currency("EUR", isDefault: true);
        Currency("CZK");
        Market("Slovakia", "SVK", "SK", "EUR");
        Market("Germany", "DEU", "DE", "EUR");
        Market("Czechia", "CZE", "CZ", "CZK");

        var markets = await Run();

        Assert.Equal(3, markets.Count);
        var flagged = Assert.Single(markets, m => m.IsDefault);
        Assert.Equal("DEU", flagged.IsoCode);
        var error = Assert.Single(_log, e => e.Level == LogLevel.Error);
        Assert.Contains("EUR", error.Message);
        Assert.Contains("DEU", error.Message);
        Assert.Contains("SVK", error.Message);
    }

    /// <summary>
    /// The default currency's country is not serviced: nothing is flagged, the list still answers,
    /// the clients fall to the first listed market, and the error names the currency nobody is on.
    /// </summary>
    [Fact]
    public async Task No_Market_On_The_Default_Currency_Flags_Nothing_And_Logs_An_Error_Naming_It()
    {
        Currency("CZK", isDefault: true);
        Currency("EUR");
        Market("Slovakia", "SVK", "SK", "EUR");

        var markets = await Run();

        var slovakia = Assert.Single(markets);
        Assert.False(slovakia.IsDefault);
        var error = Assert.Single(_log, e => e.Level == LogLevel.Error);
        Assert.Contains("CZK", error.Message);
    }

    /// <summary>
    /// No currency is the platform default at all. <c>GetDefaultAsync</c> throws on that state for its
    /// pricing callers; the landing page still answers, with one error saying so.
    /// </summary>
    [Fact]
    public async Task No_Default_Currency_At_All_Flags_Nothing_And_Logs_One_Error()
    {
        Currency("EUR");
        Market("Slovakia", "SVK", "SK", "EUR");

        var markets = await Run();

        var slovakia = Assert.Single(markets);
        Assert.False(slovakia.IsDefault);
        var error = Assert.Single(_log, e => e.Level == LogLevel.Error);
        Assert.Contains("no default currency", error.Message);
    }

    [Fact]
    public async Task Markets_Are_Ordered_By_Name()
    {
        Currency("CZK", isDefault: true);
        Currency("EUR");
        Market("Slovakia", "SVK", "SK", "EUR");
        Market("Czechia", "CZE", "CZ", "CZK");
        Market("Germany", "DEU", "DE", "EUR");

        var markets = await Run();

        Assert.Equal(["Czechia", "Germany", "Slovakia"], markets.Select(m => m.Name));
    }

    private sealed class CapturingLogger(List<(LogLevel Level, string Message)> entries) : ILogger<GetMarkets.Handler>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add((logLevel, formatter(state, exception)));
    }
}
