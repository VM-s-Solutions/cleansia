using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Features.Orders;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// ADR-0064 D1: a recurring booking at a saved address in a country nobody operates — a deactivated
/// company's market, or a delisted country — is refused <c>country.not_serviced</c> on the address
/// the customer named, the same answer the one-off booking gets from <c>OrderAddressResolver</c>.
/// A saved address the handler cannot find passes untouched so its own not-found answer is given.
/// </summary>
public class CreateRecurringBookingClosedMarketTests
{
    private const string UserId = "user-plus-closed";
    private const string OpenCountryId = "country-open";
    private const string ClosedCountryId = "country-closed";
    private const string OpenAddressId = "saved-open";
    private const string ClosedAddressId = "saved-closed";

    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();

    public CreateRecurringBookingClosedMarketTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _savedAddressRepository
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([SavedAddressIn(OpenAddressId, OpenCountryId), SavedAddressIn(ClosedAddressId, ClosedCountryId)]);
    }

    [Fact]
    public async Task A_saved_address_in_a_country_nobody_operates_is_refused_on_the_address()
    {
        var result = await CreateValidator().ValidateAsync(CommandAt(ClosedAddressId));

        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CountryNotServiced, failure.ErrorMessage);
        Assert.Equal(nameof(CreateRecurringBooking.Command.SavedAddressId), failure.PropertyName);
    }

    [Fact]
    public async Task A_saved_address_in_an_operated_country_passes()
    {
        var result = await CreateValidator().ValidateAsync(CommandAt(OpenAddressId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task A_saved_address_the_caller_does_not_hold_passes_so_the_handler_answers_not_found()
    {
        var countryRepository = new Mock<ICountryRepository>();

        var result = await CreateValidator(countryRepository.Object).ValidateAsync(CommandAt("saved-unknown"));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
        countryRepository.Verify(r => r.IsServicedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private CreateRecurringBooking.Validator CreateValidator(ICountryRepository? countryRepository = null) =>
        new(
            Mock.Of<IOrderRepository>(),
            _session.Object,
            _savedAddressRepository.Object,
            OrderMarketDoubles.Trading(CreateOrderTestData.DefaultCurrency()),
            countryRepository ?? OrderMarketDoubles.Servicing(OpenCountryId));

    private static CreateRecurringBooking.Command CommandAt(string savedAddressId) =>
        new(
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: "09:00",
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: savedAddressId,
            SelectedServiceIds: ["service-1"],
            SelectedPackageIds: [],
            PaymentType: (int)PaymentType.Card,
            StartsOn: DateTime.UtcNow.AddDays(3));

    private static SavedAddress SavedAddressIn(string savedAddressId, string countryId)
    {
        var address = Address.Create("Hlavna 1", "Bratislava", "81101", countryId);
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: false);
        saved.Id = savedAddressId;
        typeof(SavedAddress).GetProperty(nameof(SavedAddress.Address))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(saved, [address]);
        return saved;
    }
}
