using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Pins that the PARTNER order-detail response — an employee caller through the shared
/// <see cref="GetOrderDetails"/> query both partner hosts' Order/GetById serve — carries the
/// per-line service/package Translations dict exactly like the customer detail
/// (<see cref="OrderDetailMapperTranslationsTests"/>), and that untranslated historical lines
/// degrade to the frozen English snapshot Name with an empty dict instead of failing.
/// </summary>
public class GetOrderDetailsPartnerTranslationsTests
{
    private const string OrderId = "order-detail-1";
    private const string EmployeeId = "emp-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _userSessionProvider = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderEmployeePayRepository = new();
    private readonly Mock<IOrderPhotoRepository> _orderPhotoRepository = new();

    private GetOrderDetails.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _orderAccessService.Object,
            _userSessionProvider.Object,
            _payConfigRepository.Object,
            _orderEmployeePayRepository.Object,
            _orderPhotoRepository.Object);

    private void ArrangeEmployeeCaller(Order order)
    {
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orderAccessService
            .Setup(a => a.CanBrowseOrderAsync(order, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _orderAccessService
            .Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmployeeId);
        _orderAccessService
            .Setup(a => a.IsCustomerCaller())
            .Returns(false);
        _userSessionProvider
            .Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()));
        _orderEmployeePayRepository
            .Setup(r => r.GetByOrderAndEmployeeAsync(OrderId, EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderEmployeePay?)null);
        _payConfigRepository
            .Setup(r => r.GetServiceConfigsForOrderAsync(
                It.IsAny<IEnumerable<string>>(), EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeePayConfig>());
        _payConfigRepository
            .Setup(r => r.GetPackageConfigsForOrderAsync(
                It.IsAny<IEnumerable<string>>(), EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeePayConfig>());
        _orderPhotoRepository
            .Setup(r => r.GetPhotoCountByOrderIdAndTypeAsync(OrderId, PhotoType.After, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private static Order BuildOrderWith(Service service, Package package)
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial { Id = OrderId });
        order.AddSelectedServices(new[] { OrderService.Create(order, service) });
        order.AddSelectedPackages(new[] { OrderPackage.Create(order, package) });
        return order;
    }

    private static Service BuildTranslatedService()
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

    private static Package BuildTranslatedPackage()
    {
        var package = Package.Create("Deluxe", "Deluxe bundle", 500m);
        package.SetTranslation("ru", "Делюкс", "Описание пакета");
        package.SetTranslation("cs", "Deluxe balíček", "Popis");
        return package;
    }

    [Fact]
    public async Task EmployeeCaller_Detail_Carries_Service_And_Package_Translations()
    {
        var order = BuildOrderWith(BuildTranslatedService(), BuildTranslatedPackage());
        ArrangeEmployeeCaller(order);

        var result = await CreateHandler().Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var serviceLine = Assert.Single(result.Value!.SelectedServices);
        Assert.Equal("Deep Clean", serviceLine.Name);
        Assert.Equal("Генеральная уборка", serviceLine.Translations["ru"].Name);
        Assert.Equal("Generální úklid", serviceLine.Translations["cs"].Name);

        var packageLine = Assert.Single(result.Value.SelectedPackages);
        Assert.Equal("Deluxe", packageLine.Name);
        Assert.Equal("Делюкс", packageLine.Translations["ru"].Name);
        Assert.Equal("Deluxe balíček", packageLine.Translations["cs"].Name);
    }

    [Fact]
    public async Task EmployeeCaller_Untranslated_Lines_Keep_Snapshot_English_And_Empty_Translations()
    {
        var service = Service.Create("cat-1", "Windows", "Windows desc", 100m, 10m, 30);
        var package = Package.Create("Basic", "Basic bundle", 200m);
        var order = BuildOrderWith(service, package);
        ArrangeEmployeeCaller(order);

        var result = await CreateHandler().Handle(new GetOrderDetails.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var serviceLine = Assert.Single(result.Value!.SelectedServices);
        Assert.Equal("Windows", serviceLine.Name);
        Assert.Empty(serviceLine.Translations);

        var packageLine = Assert.Single(result.Value.SelectedPackages);
        Assert.Equal("Basic", packageLine.Name);
        Assert.Empty(packageLine.Translations);
    }
}
