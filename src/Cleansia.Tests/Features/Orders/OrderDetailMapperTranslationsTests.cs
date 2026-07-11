using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;
using Cleansia.TestUtilities.MockDataFactories.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Pins that the order-DETAIL projection carries the per-line service/package Translations dict
/// (T-0394) — the customer app localizes the order-detail names off it, the same way the order LIST
/// already does. The frozen snapshot Name stays as the English fallback for older orders that have
/// no translation.
/// </summary>
public class OrderDetailMapperTranslationsTests
{
    private static Service BuildService()
    {
        var service = Service.Create(
            categoryId: "cat-1",
            name: "Deep Clean",
            description: "Deep clean desc",
            basePrice: 100m,
            perRoomPrice: 10m,
            estimatedTime: 30);
        service.SetTranslation("ru", "Генеральная уборка", "Описание");
        service.SetTranslation("cs", "Generální úklid", "Popis");
        return service;
    }

    private static Package BuildPackage()
    {
        var package = Package.Create("Deluxe", "Deluxe bundle", 500m);
        package.SetTranslation("ru", "Делюкс", "Описание пакета");
        package.SetTranslation("cs", "Deluxe balíček", "Popis");
        return package;
    }

    private static Order BuildOrderWith(Service service, Package package)
    {
        var order = OrderMockFactory.Generate();
        order.AddSelectedServices(new[] { OrderService.Create(order, service) });
        order.AddSelectedPackages(new[] { OrderPackage.Create(order, package) });
        return order;
    }

    [Fact]
    public void MapToDetail_Emits_Service_Translations_PerLine()
    {
        var service = BuildService();
        var detail = BuildOrderWith(service, BuildPackage()).MapToDetail();

        var line = Assert.Single(detail.SelectedServices);
        Assert.Equal("Deep Clean", line.Name);
        Assert.Equal("Генеральная уборка", line.Translations["ru"].Name);
        Assert.Equal("Generální úklid", line.Translations["cs"].Name);
    }

    [Fact]
    public void MapToDetail_Emits_Package_Translations_PerLine()
    {
        var package = BuildPackage();
        var detail = BuildOrderWith(BuildService(), package).MapToDetail();

        var line = Assert.Single(detail.SelectedPackages);
        Assert.Equal("Deluxe", line.Name);
        Assert.Equal("Делюкс", line.Translations["ru"].Name);
        Assert.Equal("Deluxe balíček", line.Translations["cs"].Name);
    }

    [Fact]
    public void MapToDetail_Untranslated_Lines_Emit_Empty_Translations_And_Keep_English_Name()
    {
        var service = Service.Create("cat-1", "Windows", "Windows desc", 100m, 10m, 30);
        var package = Package.Create("Basic", "Basic bundle", 200m);

        var detail = BuildOrderWith(service, package).MapToDetail();

        Assert.Empty(Assert.Single(detail.SelectedServices).Translations);
        Assert.Equal("Windows", Assert.Single(detail.SelectedServices).Name);
        Assert.Empty(Assert.Single(detail.SelectedPackages).Translations);
        Assert.Equal("Basic", Assert.Single(detail.SelectedPackages).Name);
    }
}
