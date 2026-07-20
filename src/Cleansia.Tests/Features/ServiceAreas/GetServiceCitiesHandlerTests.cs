using Cleansia.Core.AppServices.Features.ServiceAreas;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Moq;

namespace Cleansia.Tests.Features.ServiceAreas;

public class GetServiceCitiesHandlerTests
{
    private readonly Mock<IServiceCityRepository> _repository = new();

    private GetServiceCities.Handler Handler() => new(_repository.Object);

    private static ServiceCity City(
        string id,
        string countryId,
        string name,
        string? zipPrefix = null,
        Country? country = null,
        bool isActive = true)
    {
        var city = ServiceCity.Create(countryId, name, zipPrefix);
        city.Id = id;
        city.IsActive = isActive;
        if (country is not null)
        {
            typeof(ServiceCity).GetProperty(nameof(ServiceCity.Country))!.SetValue(city, country);
        }

        return city;
    }

    [Fact]
    public async Task NoCountryId_ListsAllActiveCities_AndProjectsFullDto()
    {
        var czechia = Country.Create("Czechia", "CZE", isServiced: true);
        _repository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceCity>
            {
                City("city-1", "cz", "Prague", "10", czechia),
                City("city-2", "sk", "Bratislava", "81", Country.Create("Slovakia", "SVK", isServiced: true)),
            });

        var result = (await Handler().Handle(new GetServiceCities.Request(), CancellationToken.None)).ToList();

        Assert.Equal(2, result.Count);
        var prague = result[0];
        Assert.Equal("city-1", prague.Id);
        Assert.Equal("cz", prague.CountryId);
        Assert.Equal("Czechia", prague.CountryName);
        Assert.Equal("CZE", prague.CountryIsoCode);
        Assert.Equal("Prague", prague.Name);
        Assert.Equal("10", prague.ZipPrefix);
        Assert.True(prague.IsActive);

        _repository.Verify(r => r.GetByCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WithCountryId_ScopesToThatCountry()
    {
        _repository
            .Setup(r => r.GetByCountryAsync("cz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceCity>
            {
                City("city-1", "cz", "Prague"),
                City("city-3", "cz", "Brno"),
            });

        var result = (await Handler().Handle(new GetServiceCities.Request("cz"), CancellationToken.None)).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal("cz", c.CountryId));
        Assert.Equal(new[] { "Prague", "Brno" }, result.Select(c => c.Name));

        _repository.Verify(r => r.GetByCountryAsync("cz", It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmptyCountryId_IsTreatedAsUnscoped()
    {
        _repository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceCity> { City("city-1", "cz", "Prague") });

        var result = await Handler().Handle(new GetServiceCities.Request(string.Empty), CancellationToken.None);

        Assert.Single(result);
        _repository.Verify(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.GetByCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoServicedCities_ReturnsEmpty()
    {
        _repository
            .Setup(r => r.GetByCountryAsync("cz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceCity>());

        var result = await Handler().Handle(new GetServiceCities.Request("cz"), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task NullZipPrefixAndMissingCountryNav_ProjectToEmptyStringsAndNull()
    {
        _repository
            .Setup(r => r.GetByCountryAsync("cz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceCity> { City("city-1", "cz", "Prague", zipPrefix: null, country: null) });

        var dto = Assert.Single(await Handler().Handle(new GetServiceCities.Request("cz"), CancellationToken.None));

        Assert.Null(dto.ZipPrefix);
        Assert.Equal(string.Empty, dto.CountryName);
        Assert.Equal(string.Empty, dto.CountryIsoCode);
    }
}
