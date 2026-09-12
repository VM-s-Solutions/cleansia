using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// A cleaner is paid in the currency of the country they work in -- CZ is CZK, SK is EUR, PL is PLN --
/// never the platform default (owner ruling 2026-09-12). The platform-default fallback stays so no
/// money screen or approval breaks on a half-configured country, but when it fires for a country that
/// IS set it is a configuration defect, and a defect that is silent is one nobody fixes: it is logged
/// as an error naming the country. No country at all is not a defect, and is not logged.
/// </summary>
public class CurrencyResolutionServiceFallbackTests
{
    private const string CountryId = "country-pl";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurations = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly List<(LogLevel Level, string Message)> _logEntries = [];

    public CurrencyResolutionServiceFallbackTests()
    {
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = "currency-czk";
        czk.SetAsDefault(true);
        _currencies.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(czk);
        _currencies.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Currency?)null);
    }

    [Fact]
    public async Task A_Country_With_No_Configuration_Falls_Back_And_Logs_An_Error_Naming_It()
    {
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CountryConfiguration?)null);

        var resolved = await Service().ResolveCurrencyForCountryAsync(CountryId, CancellationToken.None);

        Assert.Equal("CZK", resolved.Code);
        var entry = Assert.Single(_logEntries, e => e.Level == LogLevel.Error);
        Assert.Contains(CountryId, entry.Message);
    }

    [Fact]
    public async Task A_Configured_Code_Naming_No_Currency_Falls_Back_And_Logs_An_Error()
    {
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(CountryId, "PLN", "pl", 0.23m));

        var resolved = await Service().ResolveCurrencyForCountryAsync(CountryId, CancellationToken.None);

        Assert.Equal("CZK", resolved.Code);
        var entry = Assert.Single(_logEntries, e => e.Level == LogLevel.Error);
        Assert.Contains(CountryId, entry.Message);
        Assert.Contains("PLN", entry.Message);
    }

    [Fact]
    public async Task A_Configured_Country_That_Resolves_Logs_Nothing()
    {
        var pln = Currency.Create("PLN", "zł", "Polish złoty");
        _currencies.Setup(r => r.GetByCodeAsync("PLN", It.IsAny<CancellationToken>())).ReturnsAsync(pln);
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(CountryId, "PLN", "pl", 0.23m));

        var resolved = await Service().ResolveCurrencyForCountryAsync(CountryId, CancellationToken.None);

        Assert.Same(pln, resolved);
        Assert.Empty(_logEntries);
    }

    [Fact]
    public async Task No_Work_Country_Is_Not_A_Defect_And_Logs_Nothing()
    {
        var resolved = await Service().ResolveCurrencyForCountryAsync(null, CancellationToken.None);

        Assert.Equal("CZK", resolved.Code);
        Assert.Empty(_logEntries);
        _countryConfigurations.Verify(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private CurrencyResolutionService Service() => new(
        _employees.Object,
        _countryConfigurations.Object,
        _currencies.Object,
        new CapturingLogger(_logEntries));

    private sealed class CapturingLogger(List<(LogLevel Level, string Message)> entries) : ILogger<CurrencyResolutionService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add((logLevel, formatter(state, exception)));
    }
}
