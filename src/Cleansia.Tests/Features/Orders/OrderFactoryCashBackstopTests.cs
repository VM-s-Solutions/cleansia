using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.TestUtilities.MockDataFactories.Users;
using MockQueryable;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The factory is the one construction path of one-off and recurring orders, and the recurring
/// materializer reaches it without <c>CreateOrder</c>'s validator. So the cash rule (owner ruling
/// 2026-09-24) is drawn here too, on the crew the factory itself stamps, before anything is added or
/// announced.
/// </summary>
public class OrderFactoryCashBackstopTests
{
    private const string ServiceId = "service-cash-backstop";
    private const string UserId = "user-cash-backstop";
    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();

    public OrderFactoryCashBackstopTests()
    {
        _loyaltyService
            .Setup(s => s.ResolveTierDiscountForOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierDiscountResult(0m, null));
    }

    [Theory]
    [InlineData(null, 120)]
    [InlineData(null, 60)]
    [InlineData(UserId, 121)]
    public async Task Cash_For_A_Guest_Or_A_Two_Cleaner_Job_Is_Refused_Before_The_Order_Is_Added(
        string? userId, int minutes)
    {
        SeedService(minutes);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateFactory().CreateAsync(Input(userId, PaymentType.Cash), CancellationToken.None));

        _orderRepository.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
        _adminNotifier.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(UserId, PaymentType.Cash, 120, 1)]
    [InlineData(UserId, PaymentType.Card, 121, 2)]
    [InlineData(null, PaymentType.Card, 121, 2)]
    public async Task Everything_Else_Is_Built_As_Before(
        string? userId, PaymentType paymentType, int minutes, int crew)
    {
        SeedService(minutes);

        var order = await CreateFactory().CreateAsync(Input(userId, paymentType), CancellationToken.None);

        Assert.Equal(crew, order.RequiredEmployees);
        Assert.Equal(paymentType, order.PaymentType);
        _orderRepository.Verify(r => r.Add(order), Times.Once);
    }

    private void SeedService(int minutes)
    {
        var service = Service.Create("category-cash-backstop", "Service", "Under test", minutes);
        service.Id = ServiceId;
        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { service }.AsQueryable().BuildMock());
    }

    private OrderFactory CreateFactory()
    {
        var packages = new Mock<IPackageRepository>();
        packages.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());

        return new OrderFactory(
            _orderRepository.Object,
            _serviceRepository.Object,
            packages.Object,
            ExtraRepositoryDouble.Empty(),
            CataloguePriceDoubles.Services(Czk, (ServiceId, 500m, 0m)),
            CataloguePriceDoubles.Packages(Czk),
            CataloguePriceDoubles.NoExtras(),
            PayConfigRepositoryDouble.Covering(CreateOrderTestData.CurrencyId, [ServiceId], []),
            Mock.Of<ICompanyInfoRepository>(),
            Mock.Of<ICountryConfigurationRepository>(),
            Mock.Of<IVatCalculator>(),
            _loyaltyService.Object,
            Mock.Of<IUserMembershipRepository>(),
            NoPreferredCleanerHold.Resolver,
            WorkContractResolvers.Resolver().Object,
            Mock.Of<INotificationProducer>(),
            _adminNotifier.Object,
            NullLogger<OrderFactory>.Instance);
    }

    private static CreateOrderInput Input(string? userId, PaymentType paymentType) =>
        new(
            UserId: userId,
            CustomerName: "Test Customer",
            CustomerEmail: "customer@example.com",
            CustomerPhone: "+420123456789",
            Address: AddressMockFactory.Generate(),
            Rooms: 2,
            Bathrooms: 1,
            SelectedExtraSlugs: [],
            CleaningDate: DateTime.UtcNow.AddDays(3),
            PaymentType: paymentType,
            Currency: Czk,
            SelectedServiceIds: [ServiceId],
            SelectedPackageIds: [],
            RawSubtotal: 500m,
            NowUtc: DateTime.UtcNow,
            ReservedExpressWaiver: null,
            OperatorTenantId: null);
}
