using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.SavedAddresses;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.SavedAddresses;

/// <summary>
/// Pins the saved-address coordinate bounds to the named <see cref="GeoBounds"/> constants
/// (DA-9 / conventions "no magic numbers"). Out-of-range latitude/longitude is rejected with
/// <see cref="BusinessErrorMessage.MapboxCoordsRequired"/>; the exact boundary values are accepted.
/// The named country must exist and be a market somebody operates (ADR-0064 D1): a saved address in
/// a deactivated company's country is refused <c>country.not_serviced</c> before it is stored.
/// </summary>
public class AddSavedAddressValidatorTests
{
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly AddSavedAddress.Validator _validator;

    public AddSavedAddressValidatorTests()
    {
        _validator = new AddSavedAddress.Validator(_countryRepository.Object);
    }

    private static AddSavedAddress.Command CommandWithCoords(double latitude, double longitude, string? countryId = null) =>
        new(
            Label: "Home",
            Street: "Main Street 1",
            City: "Prague",
            ZipCode: "11000",
            CountryId: countryId,
            SetAsDefault: false,
            Latitude: latitude,
            Longitude: longitude);

    private void CountryIs(string countryId, bool exists, bool serviced)
    {
        _countryRepository.Setup(r => r.ExistsAsync(countryId, It.IsAny<CancellationToken>())).ReturnsAsync(exists);
        _countryRepository.Setup(r => r.IsServicedAsync(countryId, It.IsAny<CancellationToken>())).ReturnsAsync(serviced);
    }

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

    [Fact]
    public async Task A_country_nobody_operates_is_refused_not_serviced()
    {
        CountryIs("country-closed", exists: true, serviced: false);

        var result = await _validator.ValidateAsync(CommandWithCoords(0, 0, "country-closed"));

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CountryNotServiced, failure.ErrorMessage);
        Assert.Equal(nameof(AddSavedAddress.Command.CountryId), failure.PropertyName);
    }

    [Fact]
    public async Task An_unknown_country_is_refused_as_not_existing_and_never_asked_whether_it_is_serviced()
    {
        CountryIs("country-nowhere", exists: false, serviced: false);

        var result = await _validator.ValidateAsync(CommandWithCoords(0, 0, "country-nowhere"));

        Assert.Equal(BusinessErrorMessage.NotExistingCountryWithId, Assert.Single(result.Errors).ErrorMessage);
        _countryRepository.Verify(r => r.IsServicedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_operated_country_passes()
    {
        CountryIs("country-open", exists: true, serviced: true);

        var result = await _validator.ValidateAsync(CommandWithCoords(0, 0, "country-open"));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }
}
