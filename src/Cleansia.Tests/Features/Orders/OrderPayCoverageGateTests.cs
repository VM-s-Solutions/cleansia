using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.TestUtilities.MockDataFactories.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The catalogue half of the pay gate, at the seam every order-creation path goes through.
///
/// <para>The reachable sequence this closes: an admin publishes a service, it is bookable the same
/// second (<c>Service.Create</c> leaves <c>IsActive</c> true), a customer books it, and the order lands
/// on EVERY cleaner's board with no pay quoted — because the missing row is the platform-wide one.
/// <see cref="OrderFactory"/> is the gate rather than <c>CreateOrder.Validator</c> alone: the recurring
/// materializer creates orders through the factory without ever running that validator, exactly as the
/// booked-span cap already had to be enforced in both places.</para>
/// </summary>
public class OrderPayCoverageGateTests
{
    private const string ServiceId = "svc-1";
    private const string PackageId = "pkg-1";
    private const string CurrencyId = "czk";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IVatCalculator> _vatCalculator = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();

    public OrderPayCoverageGateTests()
    {
        var service = Service.Create("cat-1", "General Cleaning", "d", 500m, 150m, estimatedTime: 120);
        service.Id = ServiceId;
        var package = Package.Create("Essential Clean", "d", 799m);
        package.Id = PackageId;

        _serviceRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { service }.AsQueryable().BuildMock());
        _packageRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { package }.AsQueryable().BuildMock());

        ArrangeConfigs();
    }

    private void ArrangeConfigs(params EmployeePayConfig[] configs) =>
        _payConfigRepository.Setup(r => r.GetAll()).Returns(configs.AsQueryable().BuildMock());

    private static EmployeePayConfig ServiceConfig(string? employeeId = null) =>
        EmployeePayConfig.CreateForService(ServiceId, 250m, CurrencyId, employeeId: employeeId);

    private static EmployeePayConfig PackageConfig(string? employeeId = null) =>
        EmployeePayConfig.CreateForPackage(PackageId, 400m, CurrencyId, employeeId: employeeId);

    private OrderFactory CreateFactory() => new(
        _orderRepository.Object,
        _serviceRepository.Object,
        _packageRepository.Object,
        _payConfigRepository.Object,
        _companyInfoRepository.Object,
        _countryConfigurationRepository.Object,
        _vatCalculator.Object,
        _loyaltyService.Object,
        _userMembershipRepository.Object,
        NoPreferredCleanerHold.Resolver,
        _notificationProducer.Object);

    private Task<Cleansia.Core.Domain.Orders.Order> CreateOrderAsync(
        IEnumerable<string>? serviceIds = null, IEnumerable<string>? packageIds = null) =>
        CreateFactory().CreateAsync(
            new CreateOrderInput(
                UserId: null,
                CustomerName: "Test Customer",
                CustomerEmail: "customer@example.com",
                CustomerPhone: "+420123456789",
                Address: AddressMockFactory.Generate(),
                Rooms: 2,
                Bathrooms: 1,
                Extras: new Dictionary<string, bool>(),
                CleaningDate: DateTime.UtcNow.AddDays(3),
                PaymentType: PaymentType.Cash,
                Currency: Currency.Create("CZK", "Kč", "Czech Koruna", 1m),
                SelectedServiceIds: serviceIds ?? [ServiceId],
                SelectedPackageIds: packageIds ?? [],
                RawSubtotal: 1000m,
                NowUtc: DateTime.UtcNow,
                ReservedExpressWaiver: null,
                PromoDiscountAmount: 0m),
            CancellationToken.None);

    [Fact]
    public async Task The_Factory_Refuses_A_Service_With_No_Platform_Wide_Config()
    {
        ArrangeConfigs();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrderAsync());

        Assert.Contains("General Cleaning", thrown.Message);
    }

    [Fact]
    public async Task The_Factory_Refuses_A_Package_With_No_Platform_Wide_Config()
    {
        ArrangeConfigs(ServiceConfig());

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateOrderAsync(packageIds: [PackageId]));

        Assert.Contains("Essential Clean", thrown.Message);
    }

    /// <summary>
    /// A per-employee override is not an answer here: the order goes to every cleaner's board, so the
    /// question the factory asks is the platform-wide one.
    /// </summary>
    [Fact]
    public async Task A_Per_Employee_Override_Does_Not_Let_An_Order_Through()
    {
        ArrangeConfigs(ServiceConfig(employeeId: "emp-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrderAsync());
    }

    [Fact]
    public async Task The_Factory_Creates_The_Order_Once_The_Selection_Is_Covered()
    {
        ArrangeConfigs(ServiceConfig(), PackageConfig());

        var order = await CreateOrderAsync(packageIds: [PackageId]);

        Assert.NotNull(order);
        Assert.Equal(1000m, order.TotalPrice);
    }

    /// <summary>
    /// Anti-vacuity: the refusal above really is the coverage rule and not the fixture failing to build
    /// an order at all — the same fixture, one config added, produces a real order.
    /// </summary>
    [Fact]
    public async Task The_Same_Fixture_Both_Refuses_And_Succeeds_On_The_Config_Alone()
    {
        ArrangeConfigs();
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrderAsync());

        ArrangeConfigs(ServiceConfig());
        Assert.NotNull(await CreateOrderAsync());
    }
}

/// <summary>
/// The customer-facing mirror of the factory backstop. Reuses <see cref="BusinessErrorMessage"/>'s
/// existing selection codes rather than minting a customer-visible key for a state that should never
/// be reachable from the wizard — the catalogue query already withholds an unconfigured entry, so a
/// caller reaching this rule is submitting an id it was never offered.
/// </summary>
public class CreateOrderPayCoverageValidatorTests
{
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public CreateOrderPayCoverageValidatorTests()
    {
        var service = Service.Create("cat-1", "General Cleaning", "d", 500m, 150m, estimatedTime: 120);
        service.Id = CreateOrderTestData.ServiceId;
        var package = Package.Create("Essential Clean", "d", 799m);
        package.Id = CreateOrderTestData.PackageId;

        _serviceRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _packageRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _serviceRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { service }.AsQueryable().BuildMock());
        _packageRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { package }.AsQueryable().BuildMock());
        _currencyRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());

        ArrangeConfigs();
    }

    private void ArrangeConfigs(params EmployeePayConfig[] configs) =>
        _payConfigRepository.Setup(r => r.GetAll()).Returns(configs.AsQueryable().BuildMock());

    private CreateOrder.Validator CreateValidator() => new(
        _packageRepository.Object,
        _serviceRepository.Object,
        _currencyRepository.Object,
        _pricingCalculator.Object,
        _orderRepository.Object,
        _userMembershipRepository.Object,
        _session.Object,
        _payConfigRepository.Object);

    [Fact]
    public async Task An_Unconfigured_Service_Fails_InvalidSelectedServices()
    {
        ArrangeConfigs(EmployeePayConfig.CreateForPackage(CreateOrderTestData.PackageId, 400m, "czk"));

        var result = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.SelectedServiceIds)
            && e.ErrorMessage == BusinessErrorMessage.InvalidSelectedServices);
    }

    [Fact]
    public async Task An_Unconfigured_Package_Fails_InvalidSelectedPackage()
    {
        ArrangeConfigs(EmployeePayConfig.CreateForService(CreateOrderTestData.ServiceId, 250m, "czk"));

        var result = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(CreateOrder.Command.SelectedPackageIds)
            && e.ErrorMessage == BusinessErrorMessage.InvalidSelectedPackage);
    }

    [Fact]
    public async Task A_Fully_Configured_Selection_Passes()
    {
        ArrangeConfigs(
            EmployeePayConfig.CreateForService(CreateOrderTestData.ServiceId, 250m, "czk"),
            EmployeePayConfig.CreateForPackage(CreateOrderTestData.PackageId, 400m, "czk"));

        var result = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand());

        Assert.True(result.IsValid);
    }
}
