using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.SavedAddresses;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.SavedAddresses;

/// <summary>
/// Characterization net for <see cref="AddSavedAddress.Handler"/>: pins the existing behavior of every
/// branch (country fallback, address reuse vs create, the duplicate-saved-address guard, default-clearing,
/// and the projected DTO) so the god-method decomposition is provably behavior-preserving.
/// </summary>
public class AddSavedAddressHandlerTests
{
    private const string CallerUserId = "user-1";

    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public AddSavedAddressHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CallerUserId);
        _savedAddressRepository
            .Setup(r => r.GetByUserAsync(CallerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _countryRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<Country>().AsQueryable().BuildMock());
    }

    private AddSavedAddress.Handler CreateHandler() => new(
        _addressRepository.Object,
        _savedAddressRepository.Object,
        _countryRepository.Object,
        _session.Object);

    private static AddSavedAddress.Command CommandWith(string? countryId, bool setAsDefault = false) =>
        new(
            Label: "Home",
            Street: "Main Street 1",
            City: "Prague",
            ZipCode: "11000",
            CountryId: countryId,
            SetAsDefault: setAsDefault,
            Latitude: 50.08,
            Longitude: 14.42);

    private static Country CountryWith(string id, string name, string isoCode)
    {
        var country = Country.Create(name, isoCode);
        country.Id = id;
        return country;
    }

    private void ArrangeCountryById(string id, string name)
    {
        _countryRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryWith(id, name, "ISO"));
    }

    [Fact]
    public async Task Supplied_CountryId_Is_Used_Directly()
    {
        ArrangeCountryById("country-supplied", "Czechia");

        var result = await CreateHandler().Handle(CommandWith("country-supplied"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("country-supplied", result.Value.CountryId);
        _countryRepository.Verify(r => r.GetByIsoCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Missing_CountryId_Falls_Back_To_Iso_CZE()
    {
        _countryRepository
            .Setup(r => r.GetByIsoCodeAsync(AddressDefaults.FallbackCountryIso, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryWith("country-cze", "Czechia", AddressDefaults.FallbackCountryIso));
        ArrangeCountryById("country-cze", "Czechia");

        var result = await CreateHandler().Handle(CommandWith(countryId: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("country-cze", result.Value.CountryId);
    }

    [Fact]
    public async Task Missing_CountryId_And_No_CZE_Falls_Back_To_First_Configured_Country()
    {
        _countryRepository
            .Setup(r => r.GetByIsoCodeAsync(AddressDefaults.FallbackCountryIso, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Country?)null);
        var first = CountryWith("country-first", "Slovakia", "SVK");
        _countryRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { first }.AsQueryable().BuildMock());
        ArrangeCountryById("country-first", "Slovakia");

        var result = await CreateHandler().Handle(CommandWith(countryId: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("country-first", result.Value.CountryId);
    }

    [Fact]
    public async Task Missing_CountryId_And_No_Countries_Throws()
    {
        _countryRepository
            .Setup(r => r.GetByIsoCodeAsync(AddressDefaults.FallbackCountryIso, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Country?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().Handle(CommandWith(countryId: null), CancellationToken.None));
    }

    [Fact]
    public async Task Existing_Address_Is_Reused_Instead_Of_Created()
    {
        var existing = Address.Create("Main Street 1", "Prague", "11000", "country-supplied", null, 50.08, 14.42);
        existing.Id = "addr-existing";
        _addressRepository
            .Setup(r => r.GetAddressAsync("Main Street 1", "Prague", "11000", "country-supplied", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _addressRepository
            .Setup(r => r.GetByIdAsync("addr-existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        ArrangeCountryById("country-supplied", "Czechia");

        var result = await CreateHandler().Handle(CommandWith("country-supplied"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _addressRepository.Verify(r => r.Add(It.IsAny<Address>()), Times.Never);
    }

    [Fact]
    public async Task New_Address_Is_Added_When_None_Matches()
    {
        _addressRepository
            .Setup(r => r.GetAddressAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Address?)null);
        _addressRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Address?)null);
        ArrangeCountryById("country-supplied", "Czechia");

        var result = await CreateHandler().Handle(CommandWith("country-supplied"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _addressRepository.Verify(r => r.Add(It.IsAny<Address>()), Times.Once);
        _savedAddressRepository.Verify(r => r.Add(It.IsAny<SavedAddress>()), Times.Once);
    }

    [Fact]
    public async Task Duplicate_Saved_Address_Returns_Failure_With_Street_Code()
    {
        var existing = Address.Create("Main Street 1", "Prague", "11000", "country-supplied", null, 50.08, 14.42);
        existing.Id = "addr-dup";
        _addressRepository
            .Setup(r => r.GetAddressAsync("Main Street 1", "Prague", "11000", "country-supplied", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var alreadySaved = SavedAddress.Create(CallerUserId, "addr-dup", "Home", isDefault: false);
        _savedAddressRepository
            .Setup(r => r.GetByUserAsync(CallerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([alreadySaved]);

        var result = await CreateHandler().Handle(CommandWith("country-supplied"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(nameof(AddSavedAddress.Command.Street), result.Error!.Code);
        Assert.Equal(BusinessErrorMessage.SavedAddressAlreadyExists, result.Error.Message);
        _savedAddressRepository.Verify(r => r.Add(It.IsAny<SavedAddress>()), Times.Never);
    }

    [Fact]
    public async Task ClearDefault_Is_Invoked_Only_When_SetAsDefault_Is_True()
    {
        ArrangeCountryById("country-supplied", "Czechia");

        await CreateHandler().Handle(CommandWith("country-supplied", setAsDefault: false), CancellationToken.None);
        _savedAddressRepository.Verify(r => r.ClearDefaultForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        await CreateHandler().Handle(CommandWith("country-supplied", setAsDefault: true), CancellationToken.None);
        _savedAddressRepository.Verify(r => r.ClearDefaultForUserAsync(CallerUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Returned_Dto_Is_Built_Field_For_Field_From_Saved_Address_And_Country()
    {
        ArrangeCountryById("country-supplied", "Czechia");

        var result = await CreateHandler().Handle(CommandWith("country-supplied", setAsDefault: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal("Home", dto.Label);
        Assert.Equal("Main Street 1", dto.Street);
        Assert.Equal("Prague", dto.City);
        Assert.Equal("11000", dto.ZipCode);
        Assert.Null(dto.State);
        Assert.Equal("country-supplied", dto.CountryId);
        Assert.Equal("Czechia", dto.Country);
        Assert.Equal(50.08, dto.Latitude);
        Assert.Equal(14.42, dto.Longitude);
        Assert.True(dto.IsDefault);
    }
}
