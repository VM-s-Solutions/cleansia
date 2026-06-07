using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.SavedAddresses;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.SavedAddresses;

/// <summary>
/// Pins the saved-address coordinate bounds to the named <see cref="GeoBounds"/> constants
/// (DA-9 / conventions "no magic numbers"). Out-of-range latitude/longitude is rejected with
/// <see cref="BusinessErrorMessage.MapboxCoordsRequired"/>; the exact boundary values are accepted.
/// </summary>
public class AddSavedAddressValidatorTests
{
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly AddSavedAddress.Validator _validator;

    public AddSavedAddressValidatorTests()
    {
        _validator = new AddSavedAddress.Validator(_countryRepository.Object);
    }

    private static AddSavedAddress.Command CommandWithCoords(double latitude, double longitude) =>
        new(
            Label: "Home",
            Street: "Main Street 1",
            City: "Prague",
            ZipCode: "11000",
            CountryId: null,
            SetAsDefault: false,
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
