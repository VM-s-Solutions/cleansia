using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Unit tests for <see cref="OrderAddressResolver"/> — the address-resolution + serviced-area
/// collaborator extracted from <c>CreateOrder.Handler</c>. Covers each error branch the
/// resolver owns (saved-row not found / cross-user, country no longer serviced, country required,
/// city not serviced) plus the geocode-on-missing-coordinates seam, so the extraction is proven to
/// carry the same behavior the handler characterization suite pins.
/// </summary>
public class OrderAddressResolverTests
{
    private const string UserId = "user-1";

    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<IServiceCityRepository> _serviceCityRepository = new();
    private readonly Mock<IAddressGeocoder> _addressGeocoder = new();

    public OrderAddressResolverTests()
    {
        _countryRepository
            .Setup(r => r.IsServicedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _serviceCityRepository
            .Setup(r => r.CityIsServicedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private OrderAddressResolver CreateResolver() =>
        new(
            _addressRepository.Object,
            _savedAddressRepository.Object,
            _countryRepository.Object,
            _serviceCityRepository.Object,
            _addressGeocoder.Object);

    private void ArrangeSavedAddress(string savedAddressId, string ownerUserId, Address? resolved = null)
    {
        var saved = SavedAddressMockFactory.Generate(new SavedAddressMockFactory.SavedAddressPartial
        {
            Id = savedAddressId,
            UserId = ownerUserId,
            AddressId = "address-1",
        });
        _savedAddressRepository
            .Setup(r => r.GetByIdAsync(savedAddressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);
        _addressRepository
            .Setup(r => r.GetByIdAsync("address-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolved ?? AddressMockFactory.Generate(
                new AddressMockFactory.AddressPartial { Latitude = 50.08, Longitude = 14.43 }));
    }

    [Fact]
    public async Task SavedAddress_NotFound_ReturnsNotFound()
    {
        _savedAddressRepository
            .Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SavedAddress?)null);
        var command = CreateOrderTestData.ValidCommand(savedAddressId: "missing");

        var result = await CreateResolver().ResolveAsync(command, UserId, CancellationToken.None);

        Assert.Null(result.Address);
        Assert.Equal(BusinessErrorMessage.NotFound, result.Failure!.Message);
    }

    [Fact]
    public async Task SavedAddress_OwnedByDifferentUser_ReturnsNotFound()
    {
        ArrangeSavedAddress("saved-1", ownerUserId: "another-user");
        var command = CreateOrderTestData.ValidCommand(savedAddressId: "saved-1");

        var result = await CreateResolver().ResolveAsync(command, UserId, CancellationToken.None);

        Assert.Null(result.Address);
        Assert.Equal(BusinessErrorMessage.NotFound, result.Failure!.Message);
    }

    [Fact]
    public async Task SavedAddress_CountryNoLongerServiced_ReturnsCountryNotServiced()
    {
        ArrangeSavedAddress("saved-1", ownerUserId: UserId);
        _countryRepository
            .Setup(r => r.IsServicedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var command = CreateOrderTestData.ValidCommand(savedAddressId: "saved-1");

        var result = await CreateResolver().ResolveAsync(command, UserId, CancellationToken.None);

        Assert.Null(result.Address);
        Assert.Equal(BusinessErrorMessage.CountryNotServiced, result.Failure!.Message);
    }

    [Fact]
    public async Task InlineAddress_CountryNotServiced_ReturnsCountryNotServiced()
    {
        _countryRepository
            .Setup(r => r.IsServicedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var command = CreateOrderTestData.ValidCommand();

        var result = await CreateResolver().ResolveAsync(command, UserId, CancellationToken.None);

        Assert.Null(result.Address);
        Assert.Equal(BusinessErrorMessage.CountryNotServiced, result.Failure!.Message);
        Assert.Equal(nameof(AddressMockFactory.AddressPartial.CountryId), result.Failure.Code);
    }

    [Fact]
    public async Task InlineAddress_NoCountry_MultipleServiced_ReturnsCountryRequired()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: null));
        _countryRepository
            .Setup(r => r.GetServicedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Country>
            {
                Country.Create("Czechia", "CZ", isServiced: true),
                Country.Create("Slovakia", "SK", isServiced: true),
            });

        var result = await CreateResolver().ResolveAsync(command, UserId, CancellationToken.None);

        Assert.Null(result.Address);
        Assert.Equal(BusinessErrorMessage.CountryRequired, result.Failure!.Message);
    }

    [Fact]
    public async Task InlineAddress_NoCountry_SingleServiced_FallsBackToThatCountry()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: null));
        var only = Country.Create("Czechia", "CZ", isServiced: true);
        _countryRepository
            .Setup(r => r.GetServicedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Country> { only });

        var result = await CreateResolver().ResolveAsync(command, UserId, CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.NotNull(result.Address);
        Assert.Equal(only.Id, result.Address!.CountryId);
    }

    [Fact]
    public async Task CityNotServiced_ReturnsCityNotServiced()
    {
        _serviceCityRepository
            .Setup(r => r.CityIsServicedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var command = CreateOrderTestData.ValidCommand();

        var result = await CreateResolver().ResolveAsync(command, UserId, CancellationToken.None);

        Assert.Null(result.Address);
        Assert.Equal(BusinessErrorMessage.CityNotServiced, result.Failure!.Message);
        Assert.Equal(nameof(Address.City), result.Failure.Code);
    }

    [Fact]
    public async Task MissingCoordinates_TriggersGeocode()
    {
        ArrangeSavedAddress("saved-1", ownerUserId: UserId,
            resolved: AddressMockFactory.Generate(
                new AddressMockFactory.AddressPartial { Latitude = null, Longitude = null }));
        var command = CreateOrderTestData.ValidCommand(savedAddressId: "saved-1");

        var result = await CreateResolver().ResolveAsync(command, UserId, CancellationToken.None);

        Assert.Null(result.Failure);
        _addressGeocoder.Verify(
            g => g.PopulateCoordinatesAsync(It.IsAny<Address>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PresentCoordinates_SkipsGeocode()
    {
        ArrangeSavedAddress("saved-1", ownerUserId: UserId,
            resolved: AddressMockFactory.Generate(
                new AddressMockFactory.AddressPartial { Latitude = 50.08, Longitude = 14.43 }));
        var command = CreateOrderTestData.ValidCommand(savedAddressId: "saved-1");

        var result = await CreateResolver().ResolveAsync(command, UserId, CancellationToken.None);

        Assert.Null(result.Failure);
        _addressGeocoder.Verify(
            g => g.PopulateCoordinatesAsync(It.IsAny<Address>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
