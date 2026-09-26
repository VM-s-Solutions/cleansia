using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Users;
using MockQueryable;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0068 D1 (Verification #2) — the one production writer of an order stamps it with the
/// contract-for-work document in force for the ORDER's market at booking, and refuses to book when
/// nothing is in force: an order no contract can form on must not exist, and the recurring
/// materializer reaches this factory without <c>CreateOrder</c>'s validator.
/// </summary>
public sealed class OrderFactoryWorkContractStampTests
{
    private const string ServiceId = "svc-wc-1";
    private const string CurrencyId = "czk";

    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IVatCalculator> _vatCalculator = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();
    private readonly Mock<ILegalDocumentResolver> _legalDocumentResolver = new();

    public OrderFactoryWorkContractStampTests()
    {
        var service = Service.Create("cat-1", "General Cleaning", "d", estimatedTime: 120);
        service.Id = ServiceId;
        _serviceRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { service }.AsQueryable().BuildMock());
        _packageRepository.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Core.Domain.Packages.Package>().AsQueryable().BuildMock());
        _payConfigRepository.Setup(r => r.GetAll())
            .Returns(new[] { EmployeePayConfig.CreateForService(ServiceId, 250m, CurrencyId) }.AsQueryable().BuildMock());
    }

    private OrderFactory CreateFactory() => new(
        _orderRepository.Object,
        _serviceRepository.Object,
        _packageRepository.Object,
        ExtraRepositoryDouble.Empty(),
        CataloguePriceDoubles.Services(Czk, (ServiceId, 500m, 100m)),
        CataloguePriceDoubles.NoPackages(),
        CataloguePriceDoubles.NoExtras(),
        _payConfigRepository.Object,
        _companyInfoRepository.Object,
        _countryConfigurationRepository.Object,
        _vatCalculator.Object,
        _loyaltyService.Object,
        _userMembershipRepository.Object,
        NoPreferredCleanerHold.Resolver,
        _legalDocumentResolver.Object,
        Mock.Of<INotificationProducer>(),
        Mock.Of<IAdminNotifier>(),
        NullLogger<OrderFactory>.Instance);

    private Task<Core.Domain.Orders.Order> CreateOrderAsync(Address address) =>
        CreateFactory().CreateAsync(
            new CreateOrderInput(
                UserId: null,
                CustomerName: "Test Customer",
                CustomerEmail: "customer@example.com",
                CustomerPhone: "+420123456789",
                Address: address,
                Rooms: 2,
                Bathrooms: 1,
                SelectedExtraSlugs: [],
                CleaningDate: DateTime.UtcNow.AddDays(3),
                PaymentType: PaymentType.Card,
                Currency: Czk,
                SelectedServiceIds: [ServiceId],
                SelectedPackageIds: [],
                RawSubtotal: 1000m,
                NowUtc: DateTime.UtcNow,
                ReservedExpressWaiver: null,
                OperatorTenantId: null,
                PromoDiscountAmount: 0m),
            CancellationToken.None);

    [Fact]
    public async Task A_Booking_Is_Stamped_With_The_Document_In_Force_For_The_Orders_Market()
    {
        var address = AddressMockFactory.Generate();
        var document = WorkContractTestData.Document();
        _legalDocumentResolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.WorkContract, address.CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var order = await CreateOrderAsync(address);

        Assert.Equal(WorkContractTestData.DocumentId, order.WorkContractDocumentId);
        _legalDocumentResolver.Verify(
            r => r.ResolveInForceAsync(LegalDocumentType.WorkContract, address.CountryId, It.IsAny<CancellationToken>()),
            Times.Once);
        _orderRepository.Verify(r => r.Add(order), Times.Once);
    }

    [Fact]
    public async Task With_Nothing_In_Force_The_Factory_Throws_And_No_Order_Exists()
    {
        var address = AddressMockFactory.Generate();
        _legalDocumentResolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.WorkContract, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LegalDocument?)null);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrderAsync(address));

        Assert.Contains("work-contract", thrown.Message);
        Assert.Contains(address.CountryId, thrown.Message);
        _orderRepository.Verify(r => r.Add(It.IsAny<Core.Domain.Orders.Order>()), Times.Never);
    }

    [Fact]
    public async Task The_Same_Fixture_Both_Refuses_And_Stamps_On_The_Document_Alone()
    {
        var address = AddressMockFactory.Generate();
        _legalDocumentResolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.WorkContract, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LegalDocument?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateOrderAsync(address));

        _legalDocumentResolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.WorkContract, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkContractTestData.Document());

        Assert.Equal(WorkContractTestData.DocumentId, (await CreateOrderAsync(address)).WorkContractDocumentId);
    }
}
