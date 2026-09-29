using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Tests.Features.PayConfig;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The server holds every booking to the start-time window, in the clock of the service address's market,
/// so an API caller or a stale client cannot book 03:07 or a slot a year out.
/// </summary>
public sealed class CreateOrderBookingWindowTests
{
    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurations = new();

    public CreateOrderBookingWindowTests()
    {
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync("cz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create("cz", "CZK", "cs", 0.21m, timeZoneId: "Europe/Prague"));
    }

    [Fact]
    public async Task A_Quarter_Hour_Inside_The_Markets_Day_Is_Accepted()
    {
        var result = await Validator().ValidateAsync(BookingAt(PragueTime(daysAhead: 3, hour: 10, minute: 15)));

        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CleaningDateOutsideBookingWindow);
    }

    [Theory]
    [InlineData(3, 7)]
    [InlineData(7, 45)]
    [InlineData(20, 0)]
    [InlineData(10, 7)]
    public async Task A_Start_Outside_The_Window_Or_Off_The_Grid_Is_Refused(int hour, int minute)
    {
        var result = await Validator().ValidateAsync(BookingAt(PragueTime(daysAhead: 3, hour, minute)));

        AssertOutsideWindow(result);
    }

    /// <summary>
    /// 19:30 on the server's UTC clock is inside the day; in Prague it is 20:30 or 21:30, which is not.
    /// </summary>
    [Fact]
    public async Task The_Window_Is_Read_In_The_Service_Address_Market_Not_In_Utc()
    {
        var utcNineteenThirty = DateTime.UtcNow.Date.AddDays(3).AddHours(19).AddMinutes(30);

        var result = await Validator().ValidateAsync(BookingAt(utcNineteenThirty));

        AssertOutsideWindow(result);
    }

    [Fact]
    public async Task A_Start_Beyond_Sixty_Days_Is_Refused()
    {
        var result = await Validator().ValidateAsync(BookingAt(PragueTime(daysAhead: 61, hour: 10, minute: 0)));

        AssertOutsideWindow(result);
    }

    /// <summary>The lead-time refusal keeps its own key: the window rule runs after it, in one chain.</summary>
    [Fact]
    public async Task A_Start_Under_The_Lead_Time_Keeps_The_Lead_Time_Key()
    {
        var result = await Validator().ValidateAsync(BookingAt(DateTime.UtcNow.AddHours(1)));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CleaningDateBelowLeadTime);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CleaningDateOutsideBookingWindow);
    }

    private static void AssertOutsideWindow(FluentValidation.Results.ValidationResult result)
    {
        var error = Assert.Single(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CleaningDateOutsideBookingWindow);
        Assert.Equal(nameof(CreateOrder.Command.CleaningDate), error.PropertyName);
    }

    private static DateTime PragueTime(int daysAhead, int hour, int minute)
    {
        var prague = TimeZoneResolution.Resolve("Europe/Prague");
        var localDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, prague).Date.AddDays(daysAhead);
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate.AddHours(hour).AddMinutes(minute), DateTimeKind.Unspecified), prague);
    }

    private static CreateOrder.Command BookingAt(DateTime cleaningUtc) =>
        CreateOrderTestData.ValidCommand(cleaningDate: cleaningUtc);

    private CreateOrder.Validator Validator()
    {
        var services = new Mock<IServiceRepository>();
        services.Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        services.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>())).Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        var packages = new Mock<IPackageRepository>();
        packages.Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        packages.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>())).Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var calculator = new Mock<IOrderPricingCalculator>();
        calculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DirtinessLevel>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());

        return new CreateOrder.Validator(
            packages.Object,
            services.Object,
            calculator.Object,
            Mock.Of<IOrderRepository>(),
            Mock.Of<IUserMembershipRepository>(),
            Mock.Of<IUserSessionProvider>(),
            PayConfigRepositoryDouble.Holding(),
            currencies.Object,
            OrderMarketDoubles.AddressIn("cz"),
            OrderMarketDoubles.Trading(Czk),
            CataloguePriceDoubles.Services(Czk, (CreateOrderTestData.ServiceId, 500m, 100m)),
            CataloguePriceDoubles.Packages(Czk, (CreateOrderTestData.PackageId, 1000m)),
            Mock.Of<IPromoCodeService>(),
            OrderMarketDoubles.OperatedBy("cleansia-cz"),
            OrderMarketDoubles.TenantAt("cleansia-cz"),
            Mock.Of<IUserConsentRepository>(),
            CreateOrderTestData.Speaking(Constants.Language.English),
            _countryConfigurations.Object,
            Mock.Of<ILegalDocumentResolver>());
    }
}
