using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Catalog;

/// <summary>
/// "Bookable" is derived, not stored. <c>Service.Create</c> leaves <c>IsActive</c> true, so an admin
/// publishing an entry makes it bookable the same second — which is the first step of the reachable
/// sequence. The customer catalogue therefore withholds an entry with no platform-wide pay config the
/// same way it already withholds a deactivated one: a state that cannot be honoured is not offered,
/// and the write-side refusal in <c>OrderFactory</c> becomes unreachable from the wizard rather than
/// being the customer's first news of it.
/// </summary>
public class BookableCatalogueRequiresPayTests
{
    private const string ConfiguredServiceId = "svc-configured";
    private const string UnconfiguredServiceId = "svc-unconfigured";
    private const string ConfiguredPackageId = "pkg-configured";
    private const string UnconfiguredPackageId = "pkg-unconfigured";

    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IPackageRepository> _packages = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigs = new();

    public BookableCatalogueRequiresPayTests()
    {
        _services.Setup(r => r.GetAll()).Returns(new[]
        {
            ActiveService(ConfiguredServiceId, "Configured"),
            ActiveService(UnconfiguredServiceId, "Unconfigured")
        }.AsQueryable().BuildMock());

        _packages.Setup(r => r.GetAll()).Returns(new[]
        {
            ActivePackage(ConfiguredPackageId, "Configured Bundle"),
            ActivePackage(UnconfiguredPackageId, "Unconfigured Bundle")
        }.AsQueryable().BuildMock());

        _payConfigs.Setup(r => r.GetAll()).Returns(new[]
        {
            EmployeePayConfig.CreateForService(ConfiguredServiceId, 250m, "czk"),
            EmployeePayConfig.CreateForPackage(ConfiguredPackageId, 400m, "czk")
        }.AsQueryable().BuildMock());
    }

    private static Service ActiveService(string id, string name)
    {
        var service = Service.Create("cat-1", name, "d", 500m, 150m);
        service.Id = id;
        typeof(Service).GetProperty(nameof(Service.Category))!
            .SetValue(service, ServiceCategory.Create("cat-1", "Home", "d"));
        return service;
    }

    private static Package ActivePackage(string id, string name)
    {
        var package = Package.Create(name, "d", 799m);
        package.Id = id;
        return package;
    }

    [Fact]
    public async Task The_Service_Overview_Withholds_An_Entry_With_No_Platform_Wide_Config()
    {
        var handler = new GetServiceOverview.Handler(_services.Object, _payConfigs.Object);

        var items = (await handler.Handle(new GetServiceOverview.Request(), CancellationToken.None)).ToList();

        Assert.Equal(ConfiguredServiceId, Assert.Single(items).Id);
    }

    [Fact]
    public async Task The_Package_Overview_Withholds_An_Entry_With_No_Platform_Wide_Config()
    {
        var handler = new GetPackageOverview.Handler(_packages.Object, _payConfigs.Object);

        var items = (await handler.Handle(new GetPackageOverview.Request(), CancellationToken.None)).ToList();

        Assert.Equal(ConfiguredPackageId, Assert.Single(items).Id);
    }

    /// <summary>
    /// A per-employee override is not a platform-wide answer, so it must not put an entry back on the
    /// wizard — the order it would produce lands on every cleaner's board, not just that one's.
    /// </summary>
    [Fact]
    public async Task A_Per_Employee_Override_Does_Not_Make_An_Entry_Bookable()
    {
        _payConfigs.Setup(r => r.GetAll()).Returns(new[]
        {
            EmployeePayConfig.CreateForService(UnconfiguredServiceId, 250m, "czk", employeeId: "emp-1")
        }.AsQueryable().BuildMock());

        var handler = new GetServiceOverview.Handler(_services.Object, _payConfigs.Object);

        var items = await handler.Handle(new GetServiceOverview.Request(), CancellationToken.None);

        Assert.Empty(items);
    }

    /// <summary>
    /// Anti-vacuity: the same fixture returns BOTH entries once both are configured, so the filter is
    /// removing one rather than the query returning nothing for an unrelated reason.
    /// </summary>
    [Fact]
    public async Task Both_Entries_Are_Offered_Once_Both_Are_Configured()
    {
        _payConfigs.Setup(r => r.GetAll()).Returns(new[]
        {
            EmployeePayConfig.CreateForService(ConfiguredServiceId, 250m, "czk"),
            EmployeePayConfig.CreateForService(UnconfiguredServiceId, 250m, "czk")
        }.AsQueryable().BuildMock());

        var handler = new GetServiceOverview.Handler(_services.Object, _payConfigs.Object);

        var items = await handler.Handle(new GetServiceOverview.Request(), CancellationToken.None);

        Assert.Equal(2, items.Count());
    }
}
