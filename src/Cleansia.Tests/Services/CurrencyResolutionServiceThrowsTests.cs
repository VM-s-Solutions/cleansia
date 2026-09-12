using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// A cleaner is paid in the currency of the country they work in -- CZ is CZK, SK is EUR, PL is PLN --
/// and a booking is priced in the currency of the country its service address is in. Nothing is
/// guessed (owner ruling 2026-09-12, "throw instead 100%"): a NAMED country that does not resolve to a
/// real Currency is a configuration defect and the resolver throws, naming the country and the code,
/// rather than pricing or labelling money in the platform default. Only NO country at all -- the
/// customer wizard before an address is known -- is the platform default.
/// </summary>
public class CurrencyResolutionServiceThrowsTests
{
    private const string CountryId = "country-pl";
    private const string EmployeeId = "employee-1";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurations = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();

    public CurrencyResolutionServiceThrowsTests()
    {
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = "currency-czk";
        czk.SetAsDefault(true);
        _currencies.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(czk);
        _currencies.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Currency?)null);
    }

    [Fact]
    public async Task A_Country_With_No_Configuration_Throws_Naming_It()
    {
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CountryConfiguration?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service().ResolveCurrencyForCountryAsync(CountryId, CancellationToken.None));

        Assert.Contains(CountryId, ex.Message);
        VerifyNoDefaultConsulted();
    }

    [Fact]
    public async Task A_Blank_Configured_Code_Throws_Naming_The_Country()
    {
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(CountryId, " ", "pl", 0.23m));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service().ResolveCurrencyForCountryAsync(CountryId, CancellationToken.None));

        Assert.Contains(CountryId, ex.Message);
        VerifyNoDefaultConsulted();
    }

    [Fact]
    public async Task A_Configured_Code_Naming_No_Currency_Throws_Naming_The_Country_And_The_Code()
    {
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(CountryId, "PLN", "pl", 0.23m));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service().ResolveCurrencyForCountryAsync(CountryId, CancellationToken.None));

        Assert.Contains(CountryId, ex.Message);
        Assert.Contains("PLN", ex.Message);
        VerifyNoDefaultConsulted();
    }

    [Fact]
    public async Task A_Configured_Country_Resolves_To_Its_Currency_Even_When_Inactive()
    {
        var pln = Currency.Create("PLN", "zł", "Polish złoty");
        Assert.False(pln.IsActive);
        _currencies.Setup(r => r.GetByCodeAsync("PLN", It.IsAny<CancellationToken>())).ReturnsAsync(pln);
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(CountryId, "PLN", "pl", 0.23m));

        var resolved = await Service().ResolveCurrencyForCountryAsync(CountryId, CancellationToken.None);

        Assert.Same(pln, resolved);
        VerifyNoDefaultConsulted();
    }

    [Fact]
    public async Task No_Country_Is_The_Platform_Default_And_Consults_No_Configuration()
    {
        var resolved = await Service().ResolveCurrencyForCountryAsync(null, CancellationToken.None);

        Assert.Equal("CZK", resolved.Code);
        _countryConfigurations.Verify(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_Employee_With_No_Work_Country_Throws_Naming_Them()
    {
        _employees
            .Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Employee.CreateWithUser(User.CreateWithPassword(
                "cleaner@cleansia.test", "12345678Test!", "Nova", "Cleaner", Core.Domain.Enums.UserProfile.Employee)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service().ResolveCurrencyForEmployeeAsync(EmployeeId, CancellationToken.None));

        Assert.Contains(EmployeeId, ex.Message);
        VerifyNoDefaultConsulted();
    }

    [Fact]
    public async Task An_Unknown_Employee_Throws_Naming_Them()
    {
        _employees
            .Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service().ResolveCurrencyForEmployeeAsync(EmployeeId, CancellationToken.None));

        Assert.Contains(EmployeeId, ex.Message);
        VerifyNoDefaultConsulted();
    }

    [Fact]
    public async Task An_Employee_Is_Paid_In_Their_Work_Countrys_Currency()
    {
        var pln = Currency.Create("PLN", "zł", "Polish złoty");
        _currencies.Setup(r => r.GetByCodeAsync("PLN", It.IsAny<CancellationToken>())).ReturnsAsync(pln);
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(CountryId, "PLN", "pl", 0.23m));
        var employee = Employee.CreateWithUser(User.CreateWithPassword(
            "cleaner@cleansia.test", "12345678Test!", "Nova", "Cleaner", Core.Domain.Enums.UserProfile.Employee));
        employee.AssignWorkCountry(CountryId);
        _employees
            .Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var resolved = await Service().ResolveCurrencyForEmployeeAsync(EmployeeId, CancellationToken.None);

        Assert.Same(pln, resolved);
        VerifyNoDefaultConsulted();
    }

    private void VerifyNoDefaultConsulted() =>
        _currencies.Verify(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()), Times.Never);

    private CurrencyResolutionService Service() => new(
        _employees.Object,
        _countryConfigurations.Object,
        _currencies.Object);
}
