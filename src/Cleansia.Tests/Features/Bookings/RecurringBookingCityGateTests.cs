using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Features.Orders;
using FluentValidation.Results;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// Owner ruling 2026-10-25 (decision 9): a recurring schedule at a saved address in a city nobody serves is
/// refused <c>city.not_serviced</c> on create and on edit, the answer a one-off booking from that address
/// gets from <c>OrderAddressResolver</c>, asked with the address's postcode. A country nobody operates is
/// answered first, and a saved address the caller does not hold passes so the handler answers not-found.
/// </summary>
public class RecurringBookingCityGateTests
{
    private const string UserId = "user-recurring-city";
    private const string TemplateId = "tpl-recurring-city";
    private const string CountryId = "country-cz";
    private const string ClosedCountryId = "country-closed";
    private const string ServicedAddressId = "saved-praha";
    private const string UnservicedAddressId = "saved-kladno";
    private const string ClosedMarketAddressId = "saved-closed";

    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IServiceCityRepository> _serviceCityRepository = new();
    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();

    public RecurringBookingCityGateTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _savedAddressRepository
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                SavedAddressAt(ServicedAddressId, "Praha", "110 00", CountryId),
                SavedAddressAt(UnservicedAddressId, "Kladno", "272 01", CountryId),
                SavedAddressAt(ClosedMarketAddressId, "Bratislava", "811 01", ClosedCountryId),
            ]);
        _serviceCityRepository
            .Setup(r => r.CityIsServicedAsync(CountryId, "Praha", "110 00", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepository
            .Setup(r => r.GetByIdForOwnerAsync(TemplateId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Template());
        _membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingIgnoringTenantAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveMembership());
    }

    [Fact]
    public async Task Creating_A_Schedule_In_A_City_Nobody_Serves_Is_Refused_On_The_Address()
    {
        AssertRefusedOnTheAddress(await CreateValidator().ValidateAsync(CreateCommand(UnservicedAddressId)), BusinessErrorMessage.CityNotServiced);
    }

    [Fact]
    public async Task Editing_A_Schedule_Onto_A_City_Nobody_Serves_Is_Refused_On_The_Address()
    {
        AssertRefusedOnTheAddress(await UpdateValidator().ValidateAsync(UpdateCommand(UnservicedAddressId)), BusinessErrorMessage.CityNotServiced);
    }

    [Fact]
    public async Task A_Schedule_In_A_Serviced_City_Passes_On_Create_And_On_Edit()
    {
        var create = await CreateValidator().ValidateAsync(CreateCommand(ServicedAddressId));
        var update = await UpdateValidator().ValidateAsync(UpdateCommand(ServicedAddressId));

        Assert.True(create.IsValid, string.Join("; ", create.Errors.Select(e => e.ErrorMessage)));
        Assert.True(update.IsValid, string.Join("; ", update.Errors.Select(e => e.ErrorMessage)));
        _serviceCityRepository.Verify(
            r => r.CityIsServicedAsync(CountryId, "Praha", "110 00", It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task A_Country_Nobody_Operates_Is_Answered_Before_The_City()
    {
        var create = await CreateValidator().ValidateAsync(CreateCommand(ClosedMarketAddressId));
        var update = await UpdateValidator().ValidateAsync(UpdateCommand(ClosedMarketAddressId));

        AssertRefusedOnTheAddress(create, BusinessErrorMessage.CountryNotServiced);
        AssertRefusedOnTheAddress(update, BusinessErrorMessage.CountryNotServiced);
        _serviceCityRepository.Verify(
            r => r.CityIsServicedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_Saved_Address_The_Caller_Does_Not_Hold_Passes_So_The_Handler_Answers_Not_Found()
    {
        var create = await CreateValidator().ValidateAsync(CreateCommand("saved-unknown"));
        var update = await UpdateValidator().ValidateAsync(UpdateCommand("saved-unknown"));

        Assert.True(create.IsValid, string.Join("; ", create.Errors.Select(e => e.ErrorMessage)));
        Assert.True(update.IsValid, string.Join("; ", update.Errors.Select(e => e.ErrorMessage)));
    }

    private static void AssertRefusedOnTheAddress(ValidationResult result, string message)
    {
        var failure = Assert.Single(result.Errors);
        Assert.Equal(message, failure.ErrorMessage);
        Assert.Equal(nameof(CreateRecurringBooking.Command.SavedAddressId), failure.PropertyName);
    }

    private CreateRecurringBooking.Validator CreateValidator() =>
        new(
            Mock.Of<IOrderRepository>(),
            _session.Object,
            _savedAddressRepository.Object,
            OrderMarketDoubles.Trading(CreateOrderTestData.DefaultCurrency()),
            OrderMarketDoubles.Servicing(CountryId),
            _serviceCityRepository.Object,
            CatalogueDoubles.Services(),
            CatalogueDoubles.Packages(),
            Cleansia.Tests.Features.Legal.CustomerConsentDoubles.Consented(),
            Mock.Of<Cleansia.Core.AppServices.Services.Interfaces.ILegalDocumentResolver>(),
            Mock.Of<IReceivableRepository>());

    private UpdateRecurringBooking.Validator UpdateValidator() =>
        new(
            _templateRepository.Object,
            _membershipRepository.Object,
            _session.Object,
            Mock.Of<IOrderRepository>(),
            _savedAddressRepository.Object,
            OrderMarketDoubles.Trading(CreateOrderTestData.DefaultCurrency()),
            OrderMarketDoubles.Servicing(CountryId),
            _serviceCityRepository.Object,
            CatalogueDoubles.Services(),
            CatalogueDoubles.Packages(),
            Mock.Of<IReceivableRepository>());

    private static CreateRecurringBooking.Command CreateCommand(string savedAddressId) =>
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
            StartsOn: DateTime.UtcNow.AddDays(3),
            EarlyPerformanceRequested: true);

    private static UpdateRecurringBooking.Command UpdateCommand(string savedAddressId) =>
        new(
            TemplateId: TemplateId,
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

    private static SavedAddress SavedAddressAt(string savedAddressId, string city, string zipCode, string countryId)
    {
        var address = Address.Create("Hlavní 1", city, zipCode, countryId);
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: false);
        saved.Id = savedAddressId;
        typeof(SavedAddress).GetProperty(nameof(SavedAddress.Address))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(saved, [address]);
        return saved;
    }

    private static RecurringBookingTemplate Template()
    {
        var template = RecurringBookingTemplate.Create(
            userId: UserId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: System.DayOfWeek.Tuesday,
            timeOfDay: new TimeOnly(9, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: ServicedAddressId,
            selectedServiceIds: ["service-1"],
            selectedPackageIds: [],
            paymentType: PaymentType.Card,
            startsOn: DateTime.UtcNow.AddDays(1));
        template.Id = TemplateId;
        return template;
    }

    private static UserMembership ActiveMembership()
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            discountPercentage: 10m,
            allowsExpressUpgrade: true);
        return UserMembership.Create(
            userId: UserId,
            membershipPlanId: plan.Id,
            currencyId: "currency-czk",
            stripeSubscriptionId: "sub_1",
            currentPeriodStart: DateTime.UtcNow.AddDays(-1),
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
    }
}
