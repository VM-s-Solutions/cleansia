using Cleansia.Core.AppServices.Features.Extras;
using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Tests.Features.Orders;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Catalog;

/// <summary>
/// THE CATALOGUE IS BROWSED IN THE CURRENCY OF THE COUNTRY THE SERVICE ADDRESS IS IN (owner ruling
/// 2026-09-12). The three overviews take the address's country; with it the entries are priced,
/// filtered and labelled in that country's currency, and an entry priced only in another currency is
/// withheld — the same silence a deactivated or unpaid entry already gets. Without a country the
/// overview is the platform default, which is what the wizard reads before the address step.
///
/// <para>Every entry here is priced in CZK and only the "both" entry in EUR too, so with a Slovak
/// country the filter is provably removing something rather than the query returning nothing for an
/// unrelated reason.</para>
/// </summary>
public class CatalogueOverviewMarketTests
{
    private const string Slovakia = "country-svk";
    private const string BothServiceId = "svc-both";
    private const string CzkOnlyServiceId = "svc-czk";
    private const string BothPackageId = "pkg-both";
    private const string CzkOnlyPackageId = "pkg-czk";
    private const string BothExtraId = "extra-both";
    private const string CzkOnlyExtraId = "extra-czk";

    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();
    private static readonly Currency Eur = Euro();

    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IPackageRepository> _packages = new();
    private readonly Mock<IExtraRepository> _extras = new();
    private readonly ICurrencyResolutionService _markets = OrderMarketDoubles.Trading(Czk, (Slovakia, Eur));

    private readonly IServicePriceRepository _servicePrices = PricedServices();
    private readonly IPackagePriceRepository _packagePrices = PricedPackages();
    private readonly IExtraPriceRepository _extraPrices = PricedExtras();

    /// <summary>Every entry paid in both currencies, so the price filter is the only one removing anything.</summary>
    private readonly IEmployeePayConfigRepository _payConfigs = PayConfigRepositoryDouble.Holding(
        EmployeePayConfig.CreateForService(BothServiceId, 250m, Czk.Id),
        EmployeePayConfig.CreateForService(CzkOnlyServiceId, 250m, Czk.Id),
        EmployeePayConfig.CreateForService(BothServiceId, 10m, Eur.Id),
        EmployeePayConfig.CreateForService(CzkOnlyServiceId, 10m, Eur.Id),
        EmployeePayConfig.CreateForPackage(BothPackageId, 400m, Czk.Id),
        EmployeePayConfig.CreateForPackage(CzkOnlyPackageId, 400m, Czk.Id),
        EmployeePayConfig.CreateForPackage(BothPackageId, 16m, Eur.Id),
        EmployeePayConfig.CreateForPackage(CzkOnlyPackageId, 16m, Eur.Id));

    public CatalogueOverviewMarketTests()
    {
        _services.Setup(r => r.GetAll()).Returns(new[]
        {
            ActiveService(BothServiceId, "Both"),
            ActiveService(CzkOnlyServiceId, "Czech only")
        }.AsQueryable().BuildMock());
        _packages.Setup(r => r.GetAll()).Returns(new[]
        {
            ActivePackage(BothPackageId, "Both"),
            ActivePackage(CzkOnlyPackageId, "Czech only")
        }.AsQueryable().BuildMock());
        _extras.Setup(r => r.GetAll()).Returns(new[]
        {
            ActiveExtra(BothExtraId, "both"),
            ActiveExtra(CzkOnlyExtraId, "czech-only")
        }.AsQueryable().BuildMock());
    }

    private static Currency Euro()
    {
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = "currency-eur";
        eur.IsActive = true;
        return eur;
    }

    private static IServicePriceRepository PricedServices()
    {
        var mock = new Mock<IServicePriceRepository>();
        mock.Setup(r => r.GetAll()).Returns(new[]
        {
            ServicePrice.Create(BothServiceId, Czk.Id, 500m, 100m),
            ServicePrice.Create(CzkOnlyServiceId, Czk.Id, 500m, 100m),
            ServicePrice.Create(BothServiceId, Eur.Id, 20m, 4m),
        }.AsQueryable().BuildMock());
        return mock.Object;
    }

    private static IPackagePriceRepository PricedPackages()
    {
        var mock = new Mock<IPackagePriceRepository>();
        mock.Setup(r => r.GetAll()).Returns(new[]
        {
            PackagePrice.Create(BothPackageId, Czk.Id, 1000m),
            PackagePrice.Create(CzkOnlyPackageId, Czk.Id, 1000m),
            PackagePrice.Create(BothPackageId, Eur.Id, 40m),
        }.AsQueryable().BuildMock());
        return mock.Object;
    }

    private static IExtraPriceRepository PricedExtras()
    {
        var mock = new Mock<IExtraPriceRepository>();
        mock.Setup(r => r.GetAll()).Returns(new[]
        {
            ExtraPrice.Create(BothExtraId, Czk.Id, 150m),
            ExtraPrice.Create(CzkOnlyExtraId, Czk.Id, 150m),
            ExtraPrice.Create(BothExtraId, Eur.Id, 6m),
        }.AsQueryable().BuildMock());
        return mock.Object;
    }

    private static Service ActiveService(string id, string name)
    {
        var service = Service.Create("cat-1", name, "d");
        service.Id = id;
        typeof(Service).GetProperty(nameof(Service.Category))!
            .SetValue(service, ServiceCategory.Create("cat-1", "Home", "d"));
        return service;
    }

    private static Package ActivePackage(string id, string name)
    {
        var package = Package.Create(name, "d");
        package.Id = id;
        return package;
    }

    private static Extra ActiveExtra(string id, string slug)
    {
        var extra = Extra.Create(slug, slug, "d", displayOrder: 1);
        extra.Id = id;
        return extra;
    }

    private GetServiceOverview.Handler ServiceOverview() =>
        new(_services.Object, _servicePrices, _markets, _payConfigs);

    private GetPackageOverview.Handler PackageOverview() =>
        new(_packages.Object, _packagePrices, _markets, _payConfigs);

    private GetExtraOverview.Handler ExtraOverview() =>
        new(_extras.Object, _extraPrices, _markets);

    // ---------------------------------------------------------------- services

    [Fact]
    public async Task Services_With_A_Slovak_Country_Are_Priced_In_Euro_And_The_Czech_Only_Entry_Is_Withheld()
    {
        var items = (await ServiceOverview().Handle(
            new GetServiceOverview.Request(Slovakia), CancellationToken.None)).ToList();

        var only = Assert.Single(items);
        Assert.Equal(BothServiceId, only.Id);
        Assert.Equal(20m, only.BasePrice);
        Assert.Equal(4m, only.PerRoomPrice);
        Assert.Equal("EUR", only.CurrencyCode);
    }

    [Fact]
    public async Task Services_Without_A_Country_Are_The_Platform_Default()
    {
        var items = (await ServiceOverview().Handle(
            new GetServiceOverview.Request(), CancellationToken.None)).ToList();

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal("CZK", item.CurrencyCode));
        Assert.Equal(500m, items.Single(i => i.Id == BothServiceId).BasePrice);
    }

    // ---------------------------------------------------------------- packages

    [Fact]
    public async Task Packages_With_A_Slovak_Country_Are_Priced_In_Euro_And_The_Czech_Only_Entry_Is_Withheld()
    {
        var items = (await PackageOverview().Handle(
            new GetPackageOverview.Request(Slovakia), CancellationToken.None)).ToList();

        var only = Assert.Single(items);
        Assert.Equal(BothPackageId, only.Id);
        Assert.Equal(40m, only.Price);
        Assert.Equal("EUR", only.CurrencyCode);
    }

    [Fact]
    public async Task Packages_Without_A_Country_Are_The_Platform_Default()
    {
        var items = (await PackageOverview().Handle(
            new GetPackageOverview.Request(), CancellationToken.None)).ToList();

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal("CZK", item.CurrencyCode));
    }

    // ---------------------------------------------------------------- extras

    [Fact]
    public async Task Extras_With_A_Slovak_Country_Are_Priced_In_Euro_And_The_Czech_Only_Entry_Is_Withheld()
    {
        var items = (await ExtraOverview().Handle(
            new GetExtraOverview.Request(Slovakia), CancellationToken.None)).ToList();

        var only = Assert.Single(items);
        Assert.Equal(BothExtraId, only.Id);
        Assert.Equal(6m, only.Price);
        Assert.Equal("EUR", only.CurrencyCode);
    }

    [Fact]
    public async Task Extras_Without_A_Country_Are_The_Platform_Default()
    {
        var items = (await ExtraOverview().Handle(
            new GetExtraOverview.Request(), CancellationToken.None)).ToList();

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal("CZK", item.CurrencyCode));
    }
}
