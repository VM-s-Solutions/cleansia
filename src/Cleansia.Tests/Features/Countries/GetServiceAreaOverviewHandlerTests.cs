using Cleansia.Core.AppServices.Features.Countries;
using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Countries;

/// <summary>
/// The Service Area page's one read: every active country with the admin's serviced switch, the
/// default-market flag and whether a configuration exists — instead of one details request per country.
/// </summary>
public class GetServiceAreaOverviewHandlerTests
{
    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<ICountryConfigurationRepository> _configurations = new();

    private static Country NewCountry(string id, string name, string iso3, string iso2, bool isServiced, bool isActive = true)
    {
        var country = Country.Create(name, iso3, iso2, isServiced);
        country.Id = id;
        country.IsActive = isActive;
        return country;
    }

    private async Task<List<ServiceAreaCountryDto>> RunAsync(Country[] countries, CountryConfiguration[] configurations)
    {
        _countries.Setup(r => r.GetAll()).Returns(countries.AsQueryable().BuildMock());
        _configurations.Setup(r => r.GetAll()).Returns(configurations.AsQueryable().BuildMock());

        var rows = await new GetServiceAreaOverview.Handler(_countries.Object, _configurations.Object)
            .Handle(new GetServiceAreaOverview.Request(), CancellationToken.None);
        return rows.ToList();
    }

    [Fact]
    public async Task Answers_every_active_country_ordered_by_name_and_leaves_out_the_inactive_one()
    {
        var rows = await RunAsync(
            [
                NewCountry("svk", "Slovakia", "SVK", "SK", isServiced: false),
                NewCountry("aut", "Austria", "AUT", "AT", isServiced: true, isActive: false),
                NewCountry("cze", "Czechia", "CZE", "CZ", isServiced: true),
                NewCountry("pol", "Poland", "POL", "PL", isServiced: false),
            ],
            []);

        Assert.Equal(["Czechia", "Poland", "Slovakia"], rows.Select(r => r.Name));
        Assert.DoesNotContain(rows, r => r.Id == "aut");
    }

    /// <summary>
    /// The raw switch, not the effective market: Poland is switched on with no configuration behind it
    /// and still reads as serviced, because that is the value the page's toggle writes.
    /// </summary>
    [Fact]
    public async Task The_serviced_flag_is_the_switch_column_even_without_a_configuration()
    {
        var rows = await RunAsync(
            [
                NewCountry("cze", "Czechia", "CZE", "CZ", isServiced: true),
                NewCountry("pol", "Poland", "POL", "PL", isServiced: true),
                NewCountry("svk", "Slovakia", "SVK", "SK", isServiced: false),
            ],
            [
                CountryConfiguration.Create("cze", "CZK", "cs", 0.21m),
                CountryConfiguration.Create("svk", "EUR", "sk", 0.20m),
            ]);

        Assert.True(rows.Single(r => r.Id == "cze").IsServiced);
        Assert.True(rows.Single(r => r.Id == "pol").IsServiced);
        Assert.False(rows.Single(r => r.Id == "svk").IsServiced);
    }

    [Fact]
    public async Task Each_row_carries_its_own_configuration_and_default_market_flag()
    {
        var rows = await RunAsync(
            [
                NewCountry("cze", "Czechia", "CZE", "CZ", isServiced: true),
                NewCountry("pol", "Poland", "POL", "PL", isServiced: false),
                NewCountry("svk", "Slovakia", "SVK", "SK", isServiced: true),
            ],
            [
                CountryConfiguration.Create("cze", "CZK", "cs", 0.21m).SetAsDefaultMarket(true),
                CountryConfiguration.Create("svk", "EUR", "sk", 0.20m),
            ]);

        var czechia = rows.Single(r => r.Id == "cze");
        Assert.True(czechia.HasConfiguration);
        Assert.True(czechia.IsDefaultMarket);

        var slovakia = rows.Single(r => r.Id == "svk");
        Assert.True(slovakia.HasConfiguration);
        Assert.False(slovakia.IsDefaultMarket);

        var poland = rows.Single(r => r.Id == "pol");
        Assert.False(poland.HasConfiguration);
        Assert.False(poland.IsDefaultMarket);
    }

    [Fact]
    public async Task Reads_the_configurations_in_one_query_rather_than_once_per_country()
    {
        await RunAsync(
            [
                NewCountry("cze", "Czechia", "CZE", "CZ", isServiced: true),
                NewCountry("svk", "Slovakia", "SVK", "SK", isServiced: true),
            ],
            [CountryConfiguration.Create("cze", "CZK", "cs", 0.21m)]);

        _configurations.Verify(r => r.GetAll(), Times.Once);
        _configurations.Verify(r => r.GetByCountryIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _countries.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
