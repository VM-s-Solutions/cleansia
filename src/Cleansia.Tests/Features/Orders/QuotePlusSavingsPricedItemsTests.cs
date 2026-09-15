using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The Plus-savings preview prices the same basket as <see cref="QuoteOrder"/>, in the same resolved
/// currency, through the same calculator — which throws on an entry with no price row in that
/// currency. So the validator draws the same bound: every selected service and package must be priced
/// in the currency the preview will be priced in, or the wizard gets the selection key it already
/// renders instead of a 500.
/// </summary>
public class QuotePlusSavingsPricedItemsTests
{
    private const string Eur = "currency-eur";
    private const string Czechia = "country-cze";
    private const string Slovakia = "country-svk";

    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();
    private static readonly Currency EurCurrency = WithId(Currency.Create("EUR", "€", "Euro"), Eur);

    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly ICurrencyResolutionService _markets = OrderMarketDoubles.Trading(
        Czk, (Czechia, Czk), (Slovakia, EurCurrency));
    private readonly ICountryRepository _countries = OrderMarketDoubles.Servicing(Czechia, Slovakia);

    public QuotePlusSavingsPricedItemsTests()
    {
        _serviceRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _packageRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currencyRepository
            .Setup(r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Czk);
        // The span rule sums the catalogue; this suite asserts on prices, so it sums nothing.
        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());
    }

    private static Currency WithId(Currency currency, string id)
    {
        currency.Id = id;
        currency.IsActive = true;
        return currency;
    }

    /// <summary>The selection is priced in CZK only — a service the CZK catalogue offered, then a Slovak address.</summary>
    private QuotePlusSavings.Validator Validator() =>
        new(
            _serviceRepository.Object,
            _packageRepository.Object,
            _currencyRepository.Object,
            _countries,
            _markets,
            CataloguePriceDoubles.Services(Czk, ("service-1", 500m, 100m)),
            CataloguePriceDoubles.Packages(Czk, ("package-1", 1000m)));

    private static QuotePlusSavings.Query Query(
        IEnumerable<string> serviceIds, IEnumerable<string> packageIds, string? currencyId, string? countryId) =>
        new(serviceIds, packageIds, Rooms: 2, Bathrooms: 1, PlanCode: "plus-monthly",
            CurrencyId: currencyId, CountryId: countryId);

    [Fact]
    public async Task Refuses_A_Service_With_No_Price_Row_In_The_Quote_Currency()
    {
        var result = await Validator().ValidateAsync(Query(["service-1"], [], null, Slovakia));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(QuotePlusSavings.Query.SelectedServiceIds)
            && e.ErrorMessage == BusinessErrorMessage.InvalidSelectedServices);
    }

    [Fact]
    public async Task Refuses_A_Package_With_No_Price_Row_In_The_Quote_Currency()
    {
        var result = await Validator().ValidateAsync(Query([], ["package-1"], null, Slovakia));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(QuotePlusSavings.Query.SelectedPackageIds)
            && e.ErrorMessage == BusinessErrorMessage.InvalidSelectedPackage);
    }

    /// <summary>A named currency wins over the country, exactly as in QuoteOrder.</summary>
    [Fact]
    public async Task Refuses_A_Service_Unpriced_In_The_Named_Currency()
    {
        var result = await Validator().ValidateAsync(Query(["service-1"], [], Eur, Czechia));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(QuotePlusSavings.Query.SelectedServiceIds)
            && e.ErrorMessage == BusinessErrorMessage.InvalidSelectedServices);
    }

    /// <summary>Anti-vacuity: the same selection in the market it IS priced in passes the same rules.</summary>
    [Fact]
    public async Task Accepts_A_Selection_Priced_In_The_Quote_Currency()
    {
        var result = await Validator().ValidateAsync(Query(["service-1"], ["package-1"], null, Czechia));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    /// <summary>With neither field the preview is in the platform default, and the default is what the rows are read in.</summary>
    [Fact]
    public async Task Accepts_A_Selection_Priced_In_The_Platform_Default_When_Nothing_Is_Named()
    {
        var result = await Validator().ValidateAsync(Query(["service-1"], ["package-1"], null, null));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    /// <summary>A missing entry is refused on existence, before any price row is consulted.</summary>
    [Fact]
    public async Task Refuses_An_Unknown_Service_On_Existence_First()
    {
        _serviceRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Validator().ValidateAsync(Query(["service-ghost"], [], null, Czechia));

        Assert.False(result.IsValid);
        Assert.Single(result.Errors, e =>
            e.PropertyName == nameof(QuotePlusSavings.Query.SelectedServiceIds)
            && e.ErrorMessage == BusinessErrorMessage.InvalidSelectedServices);
    }
}
