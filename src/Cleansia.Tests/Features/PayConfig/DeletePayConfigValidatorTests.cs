using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.TestUtilities.MockDataFactories.Packages;
using Cleansia.TestUtilities.MockDataFactories.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.PayConfig;

/// <summary>
/// The dependent-history guard of <see cref="DeletePayConfig.Validator"/>. Pay rows don't record
/// the config they were computed under, so the guard reconstructs the dependency through the
/// config's service/package + employee scope — and must NOT block on rows that never rode this
/// config (the pre-fix bug: any OrderEmployeePay row anywhere blocked every config forever).
/// </summary>
public class DeletePayConfigValidatorTests
{
    private const string UserEmail = "admin@cleansia.cz";
    private const string PayConfigId = "pc-1";
    private const string ServiceId = "svc-1";
    private const string OtherServiceId = "svc-other";
    private const string PackageId = "pkg-1";
    private const string EmployeeId = "emp-1";
    private const string OtherEmployeeId = "emp-2";
    private const string CurrencyId = "cur-1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();

    public DeletePayConfigValidatorTests()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "A", "D");
        user.ConfirmEmail();
        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _userRepository
            .Setup(r => r.GetByEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    private DeletePayConfig.Validator CreateValidator() => new(
        _userRepository.Object,
        _session.Object,
        _payConfigRepository.Object,
        _orderPayRepository.Object);

    private void ArrangeConfig(EmployeePayConfig config)
    {
        config.Id = PayConfigId;
        _payConfigRepository
            .Setup(r => r.ExistsAsync(PayConfigId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payConfigRepository
            .Setup(r => r.GetByIdAsync(PayConfigId, It.IsAny<CancellationToken>())).ReturnsAsync(config);
    }

    private void ArrangeOrderPays(params OrderEmployeePay[] rows) =>
        _orderPayRepository.Setup(r => r.GetAll()).Returns(rows.AsQueryable().BuildMock());

    private static EmployeePayConfig ServiceConfig(string? employeeId = null) =>
        EmployeePayConfig.CreateForService(ServiceId, 100m, CurrencyId, employeeId: employeeId);

    private static OrderEmployeePay PayRowForService(string serviceId, string employeeId = EmployeeId)
    {
        var order = OrderMockFactory.Generate();
        var service = ServiceMockFactory.Generate();
        service.Id = serviceId;
        order.AddSelectedServices([OrderService.Create(order, service)]);
        return PayRow(order, employeeId);
    }

    private static OrderEmployeePay PayRowForPackage(string packageId, string employeeId = EmployeeId)
    {
        var order = OrderMockFactory.Generate();
        var package = PackageMockFactory.Generate();
        package.Id = packageId;
        order.AddSelectedPackages([OrderPackage.Create(order, package)]);
        return PayRow(order, employeeId);
    }

    private static OrderEmployeePay PayRow(Order order, string employeeId)
    {
        var pay = OrderEmployeePay.Create(order.Id, employeeId, "period-1", basePay: 100m, totalPay: 100m);
        typeof(OrderEmployeePay).GetProperty(nameof(OrderEmployeePay.Order))!.SetValue(pay, order);
        return pay;
    }

    [Fact]
    public async Task ServiceConfig_With_Only_Unrelated_OrderPays_Passes()
    {
        ArrangeConfig(ServiceConfig());
        ArrangeOrderPays(
            PayRowForService(OtherServiceId),
            PayRowForPackage(PackageId));

        var result = await CreateValidator().ValidateAsync(new DeletePayConfig.Command(PayConfigId));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ServiceConfig_With_Referencing_OrderPay_Fails_PayConfigHasOrderPays()
    {
        ArrangeConfig(ServiceConfig());
        ArrangeOrderPays(PayRowForService(ServiceId));

        var result = await CreateValidator().ValidateAsync(new DeletePayConfig.Command(PayConfigId));

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(DeletePayConfig.Command.PayConfigId)
            && e.ErrorMessage == BusinessErrorMessage.PayConfigHasOrderPays);
    }

    [Fact]
    public async Task PackageConfig_With_Referencing_OrderPay_Fails_PayConfigHasOrderPays()
    {
        ArrangeConfig(EmployeePayConfig.CreateForPackage(PackageId, 100m, CurrencyId));
        ArrangeOrderPays(PayRowForPackage(PackageId));

        var result = await CreateValidator().ValidateAsync(new DeletePayConfig.Command(PayConfigId));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PayConfigHasOrderPays);
    }

    [Fact]
    public async Task PackageConfig_With_Only_Unrelated_OrderPays_Passes()
    {
        ArrangeConfig(EmployeePayConfig.CreateForPackage(PackageId, 100m, CurrencyId));
        ArrangeOrderPays(PayRowForService(ServiceId));

        var result = await CreateValidator().ValidateAsync(new DeletePayConfig.Command(PayConfigId));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task PerEmployeeConfig_With_Only_Other_Employees_Rows_Passes()
    {
        ArrangeConfig(ServiceConfig(employeeId: EmployeeId));
        ArrangeOrderPays(PayRowForService(ServiceId, OtherEmployeeId));

        var result = await CreateValidator().ValidateAsync(new DeletePayConfig.Command(PayConfigId));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task PerEmployeeConfig_With_That_Employees_Row_Fails_PayConfigHasOrderPays()
    {
        ArrangeConfig(ServiceConfig(employeeId: EmployeeId));
        ArrangeOrderPays(PayRowForService(ServiceId, EmployeeId));

        var result = await CreateValidator().ValidateAsync(new DeletePayConfig.Command(PayConfigId));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PayConfigHasOrderPays);
    }

    [Fact]
    public async Task GlobalConfig_With_Any_Employees_Row_On_Matching_Order_Fails()
    {
        ArrangeConfig(ServiceConfig());
        ArrangeOrderPays(PayRowForService(ServiceId, OtherEmployeeId));

        var result = await CreateValidator().ValidateAsync(new DeletePayConfig.Command(PayConfigId));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.PayConfigHasOrderPays);
    }

    [Fact]
    public async Task Config_With_No_OrderPays_At_All_Passes()
    {
        ArrangeConfig(ServiceConfig());
        ArrangeOrderPays();

        var result = await CreateValidator().ValidateAsync(new DeletePayConfig.Command(PayConfigId));

        Assert.True(result.IsValid);
    }
}
