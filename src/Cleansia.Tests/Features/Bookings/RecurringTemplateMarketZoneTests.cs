using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// A schedule's day and time are the wall clock where the home is, so a client that shows the next visit
/// has to walk that zone — not the browser's. The template carries it: the zone of the saved address's
/// market, the same one the materializer converts each occurrence in, or the default market's when the
/// country has none of its own.
/// </summary>
public sealed class RecurringTemplateMarketZoneTests
{
    private const string UserId = "user-market-zone";
    private const string SavedAddressId = "saved-market-zone";

    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ISavedAddressRepository> _savedAddresses = new();
    private readonly Mock<IRecurringBookingTemplateRepository> _templates = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurations = new();

    public RecurringTemplateMarketZoneTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _savedAddresses.Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync([SavedAddressIn("country-cz")]);
        _templates.Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync([Template()]);
    }

    [Fact]
    public async Task The_Template_Carries_Its_Address_Markets_Zone()
    {
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync("country-cz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create("country-cz", "CZK", "cs", 0.21m, timeZoneId: "Europe/Prague"));

        var template = Assert.Single((await HandleAsync()).Value!);

        Assert.Equal("Europe/Prague", template.TimeZoneId);
    }

    [Fact]
    public async Task A_Market_With_No_Zone_Carries_The_Default_Markets()
    {
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync("country-cz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create("country-cz", "CZK", "cs", 0.21m));
        _countryConfigurations
            .Setup(r => r.GetDefaultMarketAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create("country-sk", "EUR", "sk", 0.23m, timeZoneId: "Europe/Bratislava"));

        var template = Assert.Single((await HandleAsync()).Value!);

        Assert.Equal("Europe/Bratislava", template.TimeZoneId);
    }

    private Task<Cleansia.Infra.Common.Validations.BusinessResult<IReadOnlyList<Core.AppServices.Features.Bookings.DTOs.RecurringBookingTemplateDto>>> HandleAsync() =>
        new GetMyRecurringBookings.Handler(
                _templates.Object,
                _savedAddresses.Object,
                _session.Object,
                CatalogueDoubles.Services(),
                CatalogueDoubles.Packages(),
                _countryConfigurations.Object)
            .Handle(new GetMyRecurringBookings.Query(), CancellationToken.None);

    private static RecurringBookingTemplate Template() =>
        RecurringBookingTemplate.Create(
            userId: UserId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: System.DayOfWeek.Tuesday,
            timeOfDay: new TimeOnly(10, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: SavedAddressId,
            selectedServiceIds: ["service-zone"],
            selectedPackageIds: [],
            paymentType: PaymentType.Card,
            startsOn: DateTime.UtcNow.AddDays(1));

    private static SavedAddress SavedAddressIn(string countryId)
    {
        var address = Address.Create("Dlouhá 12", "Praha", "11000", countryId);
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        typeof(SavedAddress).GetProperty(nameof(SavedAddress.Address))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(saved, [address]);
        return saved;
    }
}
