using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Tests.Features.Orders;
using FluentValidation.TestHelper;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// A schedule's time is the market's wall clock, so it is held to the same window a one-off booking is:
/// a quarter-hour from 08:00 to 19:45 (owner ruling 2026-09-28). An iOS wheel that sent 03:07 used to be
/// stored as-is and spawned an order at 03:07 every week. Its first date is held to the 60-day horizon.
/// </summary>
public sealed class RecurringScheduleStartWindowTests
{
    private const string UserId = "user-window";
    private const string TemplateId = "template-window";

    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRecurringBookingTemplateRepository> _templates = new();
    private readonly Mock<ISavedAddressRepository> _savedAddresses = new();

    public RecurringScheduleStartWindowTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _savedAddresses.Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    [Theory]
    [InlineData("03:07")]
    [InlineData("07:45")]
    [InlineData("20:00")]
    [InlineData("10:10")]
    public async Task Create_Refuses_A_Time_Outside_The_Window(string timeOfDay)
    {
        var result = await CreateValidator().TestValidateAsync(CreateCommand(timeOfDay, DateTime.UtcNow.AddDays(3)));

        result.ShouldHaveValidationErrorFor(x => x.TimeOfDay)
            .WithErrorMessage(BusinessErrorMessage.CleaningDateOutsideBookingWindow);
    }

    [Theory]
    [InlineData("08:00")]
    [InlineData("10:00")]
    [InlineData("19:45")]
    public async Task Create_Accepts_A_Quarter_Hour_Inside_The_Window(string timeOfDay)
    {
        var result = await CreateValidator().TestValidateAsync(CreateCommand(timeOfDay, DateTime.UtcNow.AddDays(3)));

        result.ShouldNotHaveValidationErrorFor(x => x.TimeOfDay);
    }

    [Fact]
    public async Task Create_Refuses_A_First_Date_Beyond_Sixty_Days()
    {
        var result = await CreateValidator().TestValidateAsync(CreateCommand("10:00", DateTime.UtcNow.Date.AddDays(62)));

        result.ShouldHaveValidationErrorFor(x => x.StartsOn)
            .WithErrorMessage(BusinessErrorMessage.CleaningDateOutsideBookingWindow);
    }

    [Fact]
    public async Task An_Unparseable_Time_Keeps_Its_Own_Key()
    {
        var result = await CreateValidator().TestValidateAsync(CreateCommand("ten", DateTime.UtcNow.AddDays(3)));

        result.ShouldHaveValidationErrorFor(x => x.TimeOfDay).WithErrorMessage(BusinessErrorMessage.InvalidEnumValue);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CleaningDateOutsideBookingWindow);
    }

    [Fact]
    public async Task Update_Refuses_A_Time_Outside_The_Window()
    {
        var result = await UpdateValidator().TestValidateAsync(UpdateCommand("03:07", DateTime.UtcNow.AddDays(3)));

        result.ShouldHaveValidationErrorFor(x => x.TimeOfDay)
            .WithErrorMessage(BusinessErrorMessage.CleaningDateOutsideBookingWindow);
    }

    [Fact]
    public async Task Update_Refuses_A_First_Date_Beyond_Sixty_Days()
    {
        var result = await UpdateValidator().TestValidateAsync(UpdateCommand("10:00", DateTime.UtcNow.Date.AddDays(62)));

        result.ShouldHaveValidationErrorFor(x => x.StartsOn)
            .WithErrorMessage(BusinessErrorMessage.CleaningDateOutsideBookingWindow);
    }

    /// <summary>A schedule that started long ago is still edited: the horizon bounds the future only.</summary>
    [Fact]
    public async Task Update_Accepts_A_First_Date_In_The_Past()
    {
        var result = await UpdateValidator().TestValidateAsync(UpdateCommand("10:00", DateTime.UtcNow.AddDays(-90)));

        result.ShouldNotHaveValidationErrorFor(x => x.StartsOn);
    }

    private CreateRecurringBooking.Validator CreateValidator() =>
        new(
            Mock.Of<IOrderRepository>(),
            _session.Object,
            _savedAddresses.Object,
            OrderMarketDoubles.Trading(CreateOrderTestData.DefaultCurrency()),
            OrderMarketDoubles.Servicing("country-cz"),
            CatalogueDoubles.Services(),
            CatalogueDoubles.Packages(),
            Cleansia.Tests.Features.Legal.CustomerConsentDoubles.Consented(),
            Mock.Of<Cleansia.Core.AppServices.Services.Interfaces.ILegalDocumentResolver>(),
            SavedCards.SavedCardDoubles.Guaranteed(), Mock.Of<IReceivableRepository>());

    private UpdateRecurringBooking.Validator UpdateValidator() =>
        new(_templates.Object, Mock.Of<IUserMembershipRepository>(), _session.Object,
            Mock.Of<IOrderRepository>(), _savedAddresses.Object,
            OrderMarketDoubles.Trading(CreateOrderTestData.DefaultCurrency()), OrderMarketDoubles.Servicing("country-cz"),
            CatalogueDoubles.Services(), CatalogueDoubles.Packages(),
            SavedCards.SavedCardDoubles.Guaranteed(), Mock.Of<IReceivableRepository>());

    private static CreateRecurringBooking.Command CreateCommand(string timeOfDay, DateTime startsOn) =>
        new(
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: timeOfDay,
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: "saved-window",
            SelectedServiceIds: ["service-1"],
            SelectedPackageIds: [],
            PaymentType: (int)PaymentType.Card,
            StartsOn: startsOn);

    private static UpdateRecurringBooking.Command UpdateCommand(string timeOfDay, DateTime startsOn) =>
        new(
            TemplateId: TemplateId,
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: timeOfDay,
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: "saved-window",
            SelectedServiceIds: ["service-1"],
            SelectedPackageIds: [],
            PaymentType: (int)PaymentType.Card,
            StartsOn: startsOn);
}
