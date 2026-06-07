using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.SavedAddresses;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.SavedAddresses;

/// <summary>
/// Pins the saved-address coordinate bounds to the named <see cref="GeoBounds"/> constants on the
/// update path (DA-9 / conventions "no magic numbers"). Existence + ownership are arranged to pass
/// so the assertions isolate the latitude/longitude rule.
/// </summary>
public class UpdateSavedAddressValidatorTests
{
    private const string SavedAddressId = "saved-1";
    private const string CallerUserId = "user-1";

    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly UpdateSavedAddress.Validator _validator;

    public UpdateSavedAddressValidatorTests()
    {
        var saved = SavedAddress.Create(
            userId: CallerUserId,
            addressId: "address-1",
            label: "Home",
            isDefault: false);
        saved.Id = SavedAddressId;

        _session.Setup(s => s.GetUserId()).Returns(CallerUserId);
        _savedAddressRepository
            .Setup(r => r.GetByIdAsync(SavedAddressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);

        _validator = new UpdateSavedAddress.Validator(
            _countryRepository.Object,
            _savedAddressRepository.Object,
            _session.Object);
    }

    private static UpdateSavedAddress.Command CommandWithCoords(double latitude, double longitude) =>
        new(
            SavedAddressId: SavedAddressId,
            Label: "Home",
            Street: "Main Street 1",
            City: "Prague",
            ZipCode: "11000",
            CountryId: null,
            Latitude: latitude,
            Longitude: longitude);

    [Theory]
    [InlineData(GeoBounds.LatMin - 0.0001, 0)]
    [InlineData(GeoBounds.LatMax + 0.0001, 0)]
    [InlineData(0, GeoBounds.LonMin - 0.0001)]
    [InlineData(0, GeoBounds.LonMax + 0.0001)]
    public async Task When_Coords_Out_Of_Bounds_Then_MapboxCoordsRequired_Error(double latitude, double longitude)
    {
        var result = await _validator.ValidateAsync(CommandWithCoords(latitude, longitude));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MapboxCoordsRequired);
    }

    [Theory]
    [InlineData(GeoBounds.LatMin, GeoBounds.LonMin)]
    [InlineData(GeoBounds.LatMax, GeoBounds.LonMax)]
    [InlineData(0, 0)]
    public async Task When_Coords_At_Or_Within_Bounds_Then_No_Coord_Error(double latitude, double longitude)
    {
        var result = await _validator.ValidateAsync(CommandWithCoords(latitude, longitude));

        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MapboxCoordsRequired);
    }
}
