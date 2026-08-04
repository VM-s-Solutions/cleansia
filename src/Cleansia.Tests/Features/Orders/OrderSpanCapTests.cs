using Cleansia.Core.AppServices.Common;
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
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The booked span is caller-controlled — <c>Order.EstimatedTime</c> is a plain sum over whatever
/// services and packages the request selects — and until <c>BookingPolicy.MaxBookableOrderSpanHours</c>
/// nothing bounded it. These pin the bound at the two places it is drawn: the validator (the customer
/// gets a business error) and <c>OrderFactory</c> (the backstop for callers that skip validation), and
/// pin that the two draw it in the same place.
///
/// <para>The <c>cap &lt;= floor</c> case is the load-bearing one. The cap and
/// <c>Order.MaxOrderSpanHours</c> are two numbers in two assemblies whose ORDER is the entire safety
/// argument for the overlap scan's lower bound; raising either alone silently reopens a double
/// booking, and a comment would not have failed.</para>
/// </summary>
public class OrderSpanCapTests
{
    /// <summary>
    /// The 10 shipped seed services on one order — 20.25 h. Below the cap on purpose: the cap must
    /// reject nothing a real catalog can legitimately produce (ADR-0039 D3.4).
    /// </summary>
    private const int SeededServicesOnlyMinutes = 1215;

    /// <summary>
    /// Every seeded service PLUS every seeded package's included services on one order — 58.25 h, the
    /// span CH-Q2 measured and the one the picker would otherwise have accepted as a query window.
    /// </summary>
    private const int SeededWorstCaseMinutes = 3495;

    private const string CategoryId = "category-span";
    private const string ServiceId = "service-span";
    private const string PackageId = "package-span";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IVatCalculator> _vatCalculator = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public OrderSpanCapTests()
    {
        _serviceRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _packageRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currencyRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());

        SeedCatalog(serviceMinutes: 0, packageServiceMinutes: 0);
    }

    [Fact]
    public void The_Bookable_Cap_Never_Exceeds_The_Overlap_Scan_Floor()
    {
        Assert.True(
            BookingPolicy.MaxBookableOrderSpanHours <= Order.MaxOrderSpanHours,
            $"BookingPolicy.MaxBookableOrderSpanHours ({BookingPolicy.MaxBookableOrderSpanHours} h) must stay "
            + $"at or under Order.MaxOrderSpanHours ({Order.MaxOrderSpanHours} h). The floor is where the "
            + "overlap scan starts looking; a bookable order longer than it is invisible to the TakeOrder "
            + "write gate, which is a double booking. Raise the floor first, never the cap alone.");
    }

    [Fact]
    public void The_Minutes_Constant_Is_The_Hours_Constant()
    {
        Assert.Equal(BookingPolicy.MaxBookableOrderSpanHours * 60, BookingPolicy.MaxBookableOrderSpanMinutes);
    }

    // ── OrderFactory — the enforced write gate ──

    [Fact]
    public async Task Factory_Creates_An_Order_Exactly_At_The_Cap()
    {
        SeedCatalog(serviceMinutes: BookingPolicy.MaxBookableOrderSpanMinutes, packageServiceMinutes: 0);

        var order = await CreateFactory().CreateAsync(Input(), CancellationToken.None);

        Assert.Equal(BookingPolicy.MaxBookableOrderSpanMinutes, order.EstimatedTime);
        Assert.Equal(12, order.RequiredEmployees);
        Assert.Equal(12, order.MaxEmployees);
    }

    [Fact]
    public async Task Factory_Refuses_One_Minute_Over_The_Cap()
    {
        SeedCatalog(serviceMinutes: BookingPolicy.MaxBookableOrderSpanMinutes + 1, packageServiceMinutes: 0);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => CreateFactory().CreateAsync(Input(), CancellationToken.None));
    }

    /// <summary>
    /// The refusal has to land BEFORE <c>CalculateRequiredEmployees</c>, otherwise an absurd span mints
    /// an absurd crew on its way out — 58.25 h asks for 30 cleaners against a platform seat cap of 12.
    /// </summary>
    [Fact]
    public async Task Factory_Refuses_Before_It_Mints_A_Crew()
    {
        SeedCatalog(serviceMinutes: SeededWorstCaseMinutes, packageServiceMinutes: 0);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => CreateFactory().CreateAsync(Input(), CancellationToken.None));

        _orderRepository.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task Factory_Allows_The_Whole_Seeded_Service_Catalog()
    {
        SeedCatalog(serviceMinutes: SeededServicesOnlyMinutes, packageServiceMinutes: 0);

        var order = await CreateFactory().CreateAsync(Input(), CancellationToken.None);

        Assert.Equal(SeededServicesOnlyMinutes, order.EstimatedTime);
    }

    [Fact]
    public async Task Factory_Refuses_The_Whole_Seeded_Catalog_Including_Packages()
    {
        SeedCatalog(
            serviceMinutes: SeededServicesOnlyMinutes,
            packageServiceMinutes: SeededWorstCaseMinutes - SeededServicesOnlyMinutes);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => CreateFactory().CreateAsync(Input(), CancellationToken.None));
    }

    // ── CreateOrder.Validator — the same bound, as a business error ──

    [Fact]
    public async Task Validator_Accepts_A_Booking_Exactly_At_The_Cap()
    {
        SeedCatalog(serviceMinutes: BookingPolicy.MaxBookableOrderSpanMinutes, packageServiceMinutes: 0);

        var result = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validator_Refuses_One_Minute_Over_The_Cap()
    {
        SeedCatalog(serviceMinutes: BookingPolicy.MaxBookableOrderSpanMinutes + 1, packageServiceMinutes: 0);

        var result = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderSpanExceedsMaximum);
    }

    [Fact]
    public async Task Validator_Refuses_The_Whole_Seeded_Catalog_Including_Packages()
    {
        SeedCatalog(
            serviceMinutes: SeededServicesOnlyMinutes,
            packageServiceMinutes: SeededWorstCaseMinutes - SeededServicesOnlyMinutes);

        var result = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderSpanExceedsMaximum);
    }

    /// <summary>
    /// A span the validator lets through and the factory then throws on is a 500 on the customer's
    /// booking; a span the validator rejects and the factory would have accepted is a lie. The two sum
    /// the same catalog through different code (entities in the factory, an aggregate in the
    /// validator), so the boundary is pinned rather than assumed.
    /// </summary>
    [Theory]
    [InlineData(BookingPolicy.MaxBookableOrderSpanMinutes - 1, true)]
    [InlineData(BookingPolicy.MaxBookableOrderSpanMinutes, true)]
    [InlineData(BookingPolicy.MaxBookableOrderSpanMinutes + 1, false)]
    public async Task The_Validator_And_The_Factory_Draw_The_Boundary_In_The_Same_Place(
        int totalMinutes, bool accepted)
    {
        SeedCatalog(serviceMinutes: totalMinutes - 60, packageServiceMinutes: 60);

        var validation = await CreateValidator().ValidateAsync(CreateOrderTestData.ValidCommand());
        var factoryAccepted = await FactoryAcceptsAsync();

        Assert.Equal(accepted, validation.IsValid);
        Assert.Equal(accepted, factoryAccepted);
    }

    private async Task<bool> FactoryAcceptsAsync()
    {
        try
        {
            await CreateFactory().CreateAsync(Input(), CancellationToken.None);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private void SeedCatalog(int serviceMinutes, int packageServiceMinutes)
    {
        var service = Service.Create(CategoryId, "Span Service", "Under test", 1000m, 0m, serviceMinutes);
        service.Id = ServiceId;

        var packagedService = Service.Create(
            CategoryId, "Packaged Service", "Inside the bundle", 500m, 0m, packageServiceMinutes);
        packagedService.Id = $"{ServiceId}-packaged";

        var package = Package.Create("Span Package", "Under test", 500m);
        package.Id = PackageId;
        package.AddService(packagedService);

        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { service }.AsQueryable().BuildMock());
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { package }.AsQueryable().BuildMock());
    }

    private OrderFactory CreateFactory() =>
        new(
            _orderRepository.Object,
            _serviceRepository.Object,
            _packageRepository.Object,
            _companyInfoRepository.Object,
            _countryConfigurationRepository.Object,
            _vatCalculator.Object,
            _loyaltyService.Object,
            _userMembershipRepository.Object,
            NoPreferredCleanerHold.Resolver,
            Mock.Of<INotificationProducer>());

    private CreateOrder.Validator CreateValidator() =>
        new(
            _packageRepository.Object,
            _serviceRepository.Object,
            _currencyRepository.Object,
            _pricingCalculator.Object,
            _orderRepository.Object,
            _session.Object);

    /// <summary>Anonymous, so the factory stays off the loyalty/membership lookups.</summary>
    private static CreateOrderInput Input() =>
        new(
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
            SelectedServiceIds: [ServiceId],
            SelectedPackageIds: [PackageId],
            RawSubtotal: 1500m,
            NowUtc: DateTime.UtcNow,
            ReservedExpressWaiver: null);
}
