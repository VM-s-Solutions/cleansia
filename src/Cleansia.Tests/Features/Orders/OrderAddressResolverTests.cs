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

    /// <summary>
    /// A guest has no account and so no saved addresses; a SavedAddressId on a guest booking can only
    /// name another customer's row. Refused as NotFound — the same answer a wrong id or another
    /// user's id gets — on both the address read and the country read the validator takes first.
    /// </summary>
    [Fact]
    public async Task SavedAddress_NamedByAGuest_ReturnsNotFound()
    {
        ArrangeSavedAddress("saved-1", ownerUserId: "some-customer");
        var command = CreateOrderTestData.ValidCommand(savedAddressId: "saved-1");

        var result = await CreateResolver().ResolveAsync(command, userId: string.Empty, CancellationToken.None);

        Assert.Null(result.Address);
        Assert.Equal(BusinessErrorMessage.NotFound, result.Failure!.Message);
    }

    [Fact]
    public async Task CountryOf_SavedAddress_NamedByAGuest_IsNull()
    {
        ArrangeSavedAddress("saved-1", ownerUserId: "some-customer",
            resolved: AddressMockFactory.Generate(new AddressMockFactory.AddressPartial { CountryId = "sk" }));
        var command = CreateOrderTestData.ValidCommand(savedAddressId: "saved-1");

        Assert.Null(await CreateResolver().ResolveCountryIdAsync(command, userId: null, CancellationToken.None));
        Assert.Null(await CreateResolver().ResolveCountryIdAsync(command, userId: string.Empty, CancellationToken.None));
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
                Country.Create("Czechia", "CZ", "CZ", isServiced: true),
                Country.Create("Slovakia", "SK", "SK", isServiced: true),
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
        var only = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
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

    // ---------------------------------------------------------------- ResolveCountryIdAsync

    /// <summary>
    /// The country read the validator takes BEFORE the handler runs, so the order's currency can be
    /// resolved from it: the same saved-vs-inline reading as ResolveAsync, minus the gates. A command
    /// that does not determine a country answers null and is left to ResolveAsync to refuse.
    /// </summary>
    [Fact]
    public async Task CountryOf_SavedAddress_IsTheSavedRowsCountry()
    {
        ArrangeSavedAddress("saved-1", ownerUserId: UserId,
            resolved: AddressMockFactory.Generate(new AddressMockFactory.AddressPartial { CountryId = "sk" }));
        var command = CreateOrderTestData.ValidCommand(savedAddressId: "saved-1");

        var countryId = await CreateResolver().ResolveCountryIdAsync(command, UserId, CancellationToken.None);

        Assert.Equal("sk", countryId);
    }

    [Fact]
    public async Task CountryOf_SavedAddress_OwnedByDifferentUser_IsNull()
    {
        ArrangeSavedAddress("saved-1", ownerUserId: "another-user",
            resolved: AddressMockFactory.Generate(new AddressMockFactory.AddressPartial { CountryId = "sk" }));
        var command = CreateOrderTestData.ValidCommand(savedAddressId: "saved-1");

        var countryId = await CreateResolver().ResolveCountryIdAsync(command, UserId, CancellationToken.None);

        Assert.Null(countryId);
    }

    [Fact]
    public async Task CountryOf_InlineAddress_IsTheCountryGiven()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: "sk"));

        var countryId = await CreateResolver().ResolveCountryIdAsync(command, UserId, CancellationToken.None);

        Assert.Equal("sk", countryId);
    }

    /// <summary>
    /// An unserviced country is not a market: the currency resolver throws on a country it cannot
    /// resolve, so the validator must never ask for one the platform does not operate in. Null here,
    /// and ResolveAsync refuses the booking with CountryNotServiced.
    /// </summary>
    [Fact]
    public async Task CountryOf_InlineAddress_NotServiced_IsNull()
    {
        _countryRepository
            .Setup(r => r.IsServicedAsync("ar", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: "ar"));

        var countryId = await CreateResolver().ResolveCountryIdAsync(command, UserId, CancellationToken.None);

        Assert.Null(countryId);
    }

    [Fact]
    public async Task CountryOf_SavedAddress_NotServicedAnyMore_IsNull()
    {
        _countryRepository
            .Setup(r => r.IsServicedAsync("ar", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        ArrangeSavedAddress("saved-1", ownerUserId: UserId,
            resolved: AddressMockFactory.Generate(new AddressMockFactory.AddressPartial { CountryId = "ar" }));
        var command = CreateOrderTestData.ValidCommand(savedAddressId: "saved-1");

        var countryId = await CreateResolver().ResolveCountryIdAsync(command, UserId, CancellationToken.None);

        Assert.Null(countryId);
    }

    [Fact]
    public async Task CountryOf_InlineAddress_NoCountry_SingleServiced_IsThatCountry()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: null));
        var only = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        _countryRepository
            .Setup(r => r.GetServicedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Country> { only });

        var countryId = await CreateResolver().ResolveCountryIdAsync(command, UserId, CancellationToken.None);

        Assert.Equal(only.Id, countryId);
    }

    [Fact]
    public async Task CountryOf_InlineAddress_NoCountry_MultipleServiced_IsNull()
    {
        var command = CreateOrderTestData.ValidCommand(
            customerAddress: CreateOrderTestData.InlineAddress(countryId: null));
        _countryRepository
            .Setup(r => r.GetServicedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Country>
            {
                Country.Create("Czechia", "CZ", "CZ", isServiced: true),
                Country.Create("Slovakia", "SK", "SK", isServiced: true),
            });

        var countryId = await CreateResolver().ResolveCountryIdAsync(command, UserId, CancellationToken.None);

        Assert.Null(countryId);
    }
}
