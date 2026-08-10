using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.PayConfig;

/// <summary>
/// The back door. Gating creation alone leaves the identical end state reachable from the other
/// direction: delete the last platform-wide config for a live entry and every cleaner's board goes
/// blank for it, with no order and no service ever having changed.
///
/// <para>Two conjuncts, and they are separate on purpose. <b>Last platform-wide row</b> — a per-employee
/// config, or a platform-wide one with a sibling, is safe to remove because the estimator still
/// resolves. <b>Still consulted</b> — active (so it can be booked) OR carried by an order that already
/// exists (so it is still quoted on a board). Dropping either conjunct either blocks deletions that are
/// harmless or lets through the one that is not.</para>
/// </summary>
public class DeletePayConfigCoverageTests
{
    private const string UserEmail = "admin@cleansia.cz";
    private const string PayConfigId = "pc-1";
    private const string ServiceId = "svc-1";
    private const string ServiceName = "General Cleaning";
    private const string PackageId = "pkg-1";
    private const string PackageName = "Essential Clean";
    private const string CurrencyId = "czk";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();

    public DeletePayConfigCoverageTests()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "A", "D");
        user.ConfirmEmail();
        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _userRepository
            .Setup(r => r.GetByEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _orderPayRepository.Setup(r => r.GetAll())
            .Returns(Array.Empty<OrderEmployeePay>().AsQueryable().BuildMock());

        ArrangeService(isActive: true);
        ArrangePackage(isActive: true);
        ArrangeOrders();
    }

    private void ArrangeService(bool isActive)
    {
        var service = Service.Create("cat-1", ServiceName, "d", 500m, 150m);
        service.Id = ServiceId;
        service.IsActive = isActive;
        _serviceRepository.Setup(r => r.GetAll()).Returns(new[] { service }.AsQueryable().BuildMock());
    }

    private void ArrangePackage(bool isActive)
    {
        var package = Package.Create(PackageName, "d", 799m);
        package.Id = PackageId;
        package.IsActive = isActive;
        _packageRepository.Setup(r => r.GetAll()).Returns(new[] { package }.AsQueryable().BuildMock());
    }

    private void ArrangeOrders(params Order[] orders) =>
        _orderRepository.Setup(r => r.GetAll()).Returns(orders.AsQueryable().BuildMock());

    private static Order OrderCarryingService(string serviceId)
    {
        var order = OrderMockFactory.Generate();
        var service = Service.Create("cat-1", "whatever", "d", 1m, 0m);
        service.Id = serviceId;
        order.AddSelectedServices([OrderService.Create(order, service)]);
        return order;
    }

    private void ArrangeConfigUnderTest(EmployeePayConfig config, params EmployeePayConfig[] others)
    {
        config.Id = PayConfigId;
        _payConfigRepository
            .Setup(r => r.ExistsAsync(PayConfigId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payConfigRepository
            .Setup(r => r.GetByIdAsync(PayConfigId, It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var all = new List<EmployeePayConfig> { config };
        for (var i = 0; i < others.Length; i++)
        {
            others[i].Id = $"pc-other-{i}";
            all.Add(others[i]);
        }

        _payConfigRepository.Setup(r => r.GetAll()).Returns(all.AsQueryable().BuildMock());
    }

    private DeletePayConfig.Validator CreateValidator() => new(
        _userRepository.Object,
        _session.Object,
        _payConfigRepository.Object,
        _orderPayRepository.Object,
        _serviceRepository.Object,
        _packageRepository.Object,
        _orderRepository.Object);

    private Task<FluentValidation.Results.ValidationResult> ValidateAsync() =>
        CreateValidator().ValidateAsync(new DeletePayConfig.Command(PayConfigId));

    private static EmployeePayConfig ServiceConfig(string? employeeId = null) =>
        EmployeePayConfig.CreateForService(ServiceId, 250m, CurrencyId, employeeId: employeeId);

    private static EmployeePayConfig PackageConfig(string? employeeId = null) =>
        EmployeePayConfig.CreateForPackage(PackageId, 400m, CurrencyId, employeeId: employeeId);

    [Fact]
    public async Task Deleting_The_Last_Platform_Wide_Config_For_An_Active_Service_Is_Refused()
    {
        ArrangeConfigUnderTest(ServiceConfig());

        var result = await ValidateAsync();

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(DeletePayConfig.Command.PayConfigId)
            && e.ErrorMessage == BusinessErrorMessage.PayConfigLastForLiveCatalogueEntry);
    }

    [Fact]
    public async Task The_Refusal_Names_The_Entry_It_Would_Blank()
    {
        ArrangeConfigUnderTest(ServiceConfig());

        var result = await ValidateAsync();

        Assert.Contains(result.Errors, e =>
            e.ErrorMessage == BusinessErrorMessage.PayConfigLastForLiveCatalogueEntry
            && e.ErrorCode == ServiceName);
    }

    [Fact]
    public async Task Deleting_The_Last_Platform_Wide_Config_For_An_Active_Package_Is_Refused()
    {
        ArrangeConfigUnderTest(PackageConfig());

        var result = await ValidateAsync();

        Assert.Contains(result.Errors, e =>
            e.ErrorMessage == BusinessErrorMessage.PayConfigLastForLiveCatalogueEntry
            && e.ErrorCode == PackageName);
    }

    /// <summary>Conjunct 1: a sibling platform-wide row keeps the entry quotable, so the delete is safe.</summary>
    [Fact]
    public async Task A_Platform_Wide_Config_With_A_Sibling_Is_Deletable()
    {
        ArrangeConfigUnderTest(ServiceConfig(), ServiceConfig());

        var result = await ValidateAsync();

        Assert.DoesNotContain(result.Errors, e =>
            e.ErrorMessage == BusinessErrorMessage.PayConfigLastForLiveCatalogueEntry);
    }

    /// <summary>
    /// Conjunct 1, the other half: a per-employee override is never the last word — the estimator falls
    /// back to the platform-wide row — so removing one cannot blank anybody's board.
    /// </summary>
    [Fact]
    public async Task A_Per_Employee_Override_Is_Deletable_Even_As_The_Only_Row_For_The_Entry()
    {
        ArrangeConfigUnderTest(ServiceConfig(employeeId: "emp-1"));

        var result = await ValidateAsync();

        Assert.DoesNotContain(result.Errors, e =>
            e.ErrorMessage == BusinessErrorMessage.PayConfigLastForLiveCatalogueEntry);
    }

    /// <summary>Conjunct 2: nothing consults a deactivated entry that no order carries.</summary>
    [Fact]
    public async Task A_Deactivated_Entry_With_No_Orders_Releases_Its_Last_Config()
    {
        ArrangeService(isActive: false);
        ArrangeConfigUnderTest(ServiceConfig());

        var result = await ValidateAsync();

        Assert.DoesNotContain(result.Errors, e =>
            e.ErrorMessage == BusinessErrorMessage.PayConfigLastForLiveCatalogueEntry);
    }

    /// <summary>
    /// Conjunct 2's second term, and the one a rule keyed only on IsActive would miss: an order created
    /// before the entry was retired still asks the estimator for a number every time a cleaner opens it.
    /// </summary>
    [Fact]
    public async Task A_Deactivated_Entry_Still_Carried_By_An_Order_Keeps_Its_Last_Config()
    {
        ArrangeService(isActive: false);
        ArrangeOrders(OrderCarryingService(ServiceId));
        ArrangeConfigUnderTest(ServiceConfig());

        var result = await ValidateAsync();

        Assert.Contains(result.Errors, e =>
            e.ErrorMessage == BusinessErrorMessage.PayConfigLastForLiveCatalogueEntry);
    }

    [Fact]
    public async Task An_Order_Carrying_A_Different_Entry_Does_Not_Hold_This_Config()
    {
        ArrangeService(isActive: false);
        ArrangeOrders(OrderCarryingService("svc-unrelated"));
        ArrangeConfigUnderTest(ServiceConfig());

        var result = await ValidateAsync();

        Assert.DoesNotContain(result.Errors, e =>
            e.ErrorMessage == BusinessErrorMessage.PayConfigLastForLiveCatalogueEntry);
    }
}
