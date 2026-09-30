using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The quote answers to the start-time window CreateOrder enforces, with the same key: a slot the
/// platform will not book must not first come back priced.
/// </summary>
public sealed class QuoteOrderBookingWindowTests
{
    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurations = new();

    public QuoteOrderBookingWindowTests()
    {
        _countryConfigurations
            .Setup(r => r.GetByCountryIdAsync("cz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create("cz", "CZK", "cs", 0.21m, timeZoneId: "Europe/Prague"));
    }

    [Fact]
    public async Task A_Quarter_Hour_Inside_The_Markets_Day_Is_Quoted()
    {
        var result = await Validator().ValidateAsync(QuoteAt(PragueTime(daysAhead: 3, hour: 10, minute: 15)));

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Theory]
    [InlineData(3, 7)]
    [InlineData(20, 0)]
    [InlineData(10, 7)]
    public async Task A_Start_Create_Would_Refuse_Is_Refused_At_The_Quote(int hour, int minute)
    {
        var result = await Validator().ValidateAsync(QuoteAt(PragueTime(daysAhead: 3, hour, minute)));

        AssertOutsideWindow(result);
    }

    /// <summary>19:30 on the server's UTC clock is 20:30 or 21:30 in Prague, after the last slot.</summary>
    [Fact]
    public async Task The_Window_Is_Read_In_The_Quoted_Market()
    {
        var result = await Validator().ValidateAsync(QuoteAt(DateTime.UtcNow.Date.AddDays(3).AddHours(19).AddMinutes(30)));

        AssertOutsideWindow(result);
    }

    [Fact]
    public async Task A_Start_Beyond_Sixty_Days_Is_Refused_At_The_Quote()
    {
        var result = await Validator().ValidateAsync(QuoteAt(PragueTime(daysAhead: 61, hour: 10, minute: 0)));

        AssertOutsideWindow(result);
    }

    [Fact]
    public async Task A_Quote_Without_A_Slot_Is_Not_Asked_About_The_Window()
    {
        var result = await Validator().ValidateAsync(QuoteAt(null));

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
        _countryConfigurations.VerifyNoOtherCalls();
    }

    private static void AssertOutsideWindow(FluentValidation.Results.ValidationResult result)
    {
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CleaningDateOutsideBookingWindow, error.ErrorMessage);
        Assert.Equal(nameof(QuoteOrder.Command.CleaningDate), error.PropertyName);
    }

    private static DateTime PragueTime(int daysAhead, int hour, int minute)
    {
        var prague = TimeZoneResolution.Resolve("Europe/Prague");
        var localDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, prague).Date.AddDays(daysAhead);
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate.AddHours(hour).AddMinutes(minute), DateTimeKind.Unspecified), prague);
    }

    private static QuoteOrder.Command QuoteAt(DateTime? cleaningUtc) =>
        new([CreateOrderTestData.ServiceId], [], Rooms: 2, Bathrooms: 1, CurrencyId: null,
            CleaningDate: cleaningUtc, CountryId: "cz");

    private QuoteOrder.Validator Validator()
    {
        var services = new Mock<IServiceRepository>();
        services.Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        services.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>())).Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        var packages = new Mock<IPackageRepository>();
        packages.Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        packages.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>())).Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        var currencies = new Mock<ICurrencyRepository>();
        currencies.Setup(r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        return new QuoteOrder.Validator(
            services.Object,
            packages.Object,
            currencies.Object,
            OrderMarketDoubles.Servicing("cz"),
            OrderMarketDoubles.Trading(Czk),
            CataloguePriceDoubles.Services(Czk, (CreateOrderTestData.ServiceId, 500m, 100m)),
            CataloguePriceDoubles.Packages(Czk),
            _countryConfigurations.Object);
    }
}
