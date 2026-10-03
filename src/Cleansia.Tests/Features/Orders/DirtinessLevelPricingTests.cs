using System.Text.Json;
using Cleansia.Config.Abstractions;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Cleansia.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The dirtiness level is a surcharge on the whole basket, extras included, inside the raw subtotal:
/// discounts come off lines + surcharge and express compounds on top (owner ruling 2026-09-28). The
/// quote, the create validator and the factory all price through the one calculator, so a quote is the
/// charge; the factory stores the surcharge in cents so the order's own figures reconcile.
/// </summary>
public class DirtinessLevelPricingTests
{
    private const string ServiceId = "service-dirtiness";
    private const string ExtraSlug = "inside-oven";
    private const string UserId = "user-dirtiness";
    private const decimal ServicePrice = 333.33m;
    private const decimal ExtraPrice = 49.99m;
    private const decimal LinesSubtotal = ServicePrice + ExtraPrice;

    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();

    private Service _service;
    private readonly Extra _extra = Extra.Create(ExtraSlug, "Inside oven", null);
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _memberships = new();
    private readonly List<decimal> _tierSubtotalsAsked = [];

    public DirtinessLevelPricingTests()
    {
        _service = Service.Create("category-dirtiness", "Standard clean", "Under test", 120);
        _service.Id = ServiceId;

        _loyaltyService
            .Setup(s => s.ResolveTierDiscountForOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, decimal subtotal, string _, CancellationToken _) =>
            {
                _tierSubtotalsAsked.Add(subtotal);
                return new TierDiscountResult(subtotal * 0.05m, LoyaltyTier.GoldPolisher);
            });
    }

    [Theory]
    [InlineData(DirtinessLevel.Normal, 0, 383.32)]
    [InlineData(DirtinessLevel.Increased, 57.50, 440.82)]
    [InlineData(DirtinessLevel.Heavy, 115.00, 498.32)]
    public async Task The_Level_Surcharges_The_Whole_Basket_Including_Extras_In_Cents(
        DirtinessLevel level, decimal expectedSurcharge, decimal expectedTotal)
    {
        var result = await PriceAsync(level, cleaningDateUtc: null);

        Assert.Equal(expectedSurcharge, result.DirtinessSurchargeAmount);
        Assert.Equal(expectedTotal, result.TotalPrice);
    }

    [Fact]
    public async Task Express_Compounds_On_Top_Of_Heavy_So_The_Basket_Costs_156_Percent()
    {
        var calculator = CreateCalculator(servicePrice: 1000m, extraPrice: null);

        var result = await calculator.CalculateAsync(
            [ServiceId], [], [], rooms: 0, bathrooms: 0, DirtinessLevel.Heavy, currencyId: null,
            cleaningDateUtc: DateTime.UtcNow.AddHours(3), userId: null, nowUtc: DateTime.UtcNow,
            CancellationToken.None);

        Assert.Equal(300m, result.DirtinessSurchargeAmount);
        Assert.Equal(260m, result.ExpressSurchargeAmount);
        Assert.Equal(1000m * 1.56m, result.TotalPrice);
    }

    [Fact]
    public async Task Discounts_Come_Off_The_Subtotal_With_The_Surcharge_Inside_It()
    {
        _memberships
            .Setup(r => r.GetEntitledForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembershipMockFactory.Paid(UserId));

        var order = await CreateOrderAsync(
            DirtinessLevel.Heavy, express: false, UserId, servicePrice: 1000m, extraPrice: null);

        Assert.Equal(1300m, Assert.Single(_tierSubtotalsAsked));
        Assert.Equal(65m, order.TierDiscountAmount);
        Assert.Equal(65m, order.MembershipDiscountAmount);
        Assert.Equal(1170m, order.TotalPrice);
    }

    /// <summary>
    /// Lines + dirtiness + express - discounts = the stored total, to the cent, on every branch; and the
    /// quote the customer agreed to is the price the order froze. Odd cents on purpose: an unrounded
    /// surcharge leaves a sub-cent raw subtotal and the stored figures stop adding up.
    /// </summary>
    [Theory]
    [InlineData(DirtinessLevel.Normal, false, false)]
    [InlineData(DirtinessLevel.Increased, false, false)]
    [InlineData(DirtinessLevel.Heavy, false, false)]
    [InlineData(DirtinessLevel.Increased, true, false)]
    [InlineData(DirtinessLevel.Heavy, true, false)]
    [InlineData(DirtinessLevel.Increased, false, true)]
    [InlineData(DirtinessLevel.Heavy, true, true)]
    public async Task The_Stored_Figures_Reconcile_And_The_Quote_Is_The_Charge(
        DirtinessLevel level, bool express, bool discounted)
    {
        if (discounted)
        {
            _memberships
                .Setup(r => r.GetEntitledForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(UserMembershipMockFactory.Paid(UserId));
        }
        var userId = discounted ? UserId : null;

        var order = await CreateOrderAsync(level, express, userId);
        var quote = await QuoteAsync(level, express, userId);

        var lines = order.SelectedServices.Sum(s => s.LineTotal) + order.SelectedExtras.Sum(e => e.UnitPrice);
        var discounts = (order.TierDiscountAmount ?? 0m)
            + (order.MembershipDiscountAmount ?? 0m)
            + (order.PromoDiscountAmount ?? 0m);

        Assert.Equal(level, order.DirtinessLevel);
        Assert.Equal(level, quote.DirtinessLevel);
        Assert.Equal(LinesSubtotal, lines);
        Assert.Equal(
            Cents(order.TotalPrice),
            lines + order.DirtinessSurchargeAmount + order.ExpressSurchargeAmount - discounts);
        Assert.Equal(quote.DirtinessSurchargeAmount, order.DirtinessSurchargeAmount);
        Assert.Equal(Cents(quote.FinalPriceAfterDiscount), Cents(order.TotalPrice));
    }

    /// <summary>
    /// The order keeps the rate it was booked at beside the surcharge that rate produced, so pay reads the
    /// booking's rate after any later change to the policy.
    /// </summary>
    [Theory]
    [InlineData(DirtinessLevel.Normal)]
    [InlineData(DirtinessLevel.Increased)]
    [InlineData(DirtinessLevel.Heavy)]
    public async Task The_Order_Stores_The_Rate_It_Was_Booked_At(DirtinessLevel level)
    {
        var order = await CreateOrderAsync(level, express: false, userId: null);

        Assert.Equal(BookingPolicy.DirtinessSurchargeRate(level), order.DirtinessRate);
        Assert.Equal(Cents(LinesSubtotal * order.DirtinessRate), order.DirtinessSurchargeAmount);
    }

    /// <summary>
    /// The level and the per-room minutes lengthen the booked time (owner rulings 2026-09-28), and the
    /// quote states the length and crew the order is then staffed with, so a client re-evaluates cash
    /// from the quote. The home here is two rooms and a bathroom.
    /// </summary>
    [Theory]
    [InlineData(DirtinessLevel.Normal, 0, 120, 1)]
    [InlineData(DirtinessLevel.Increased, 0, 138, 2)]
    [InlineData(DirtinessLevel.Heavy, 0, 156, 2)]
    [InlineData(DirtinessLevel.Normal, 10, 150, 2)]
    [InlineData(DirtinessLevel.Heavy, 10, 195, 2)]
    public async Task The_Booked_Time_Follows_The_Level_And_The_Home_And_The_Quote_States_The_Crew(
        DirtinessLevel level, int minutesPerRoom, int expectedMinutes, int expectedCrew)
    {
        _service = Service.Create("category-dirtiness", "Standard clean", "Under test", 120, minutesPerRoom);
        _service.Id = ServiceId;

        var quote = await QuoteAsync(level, express: false, userId: null);
        var order = await CreateOrderAsync(level, express: false, userId: null);

        Assert.Equal(expectedMinutes, quote.EstimatedDurationMinutes);
        Assert.Equal(expectedCrew, quote.RequiredEmployees);
        Assert.Equal(expectedMinutes, order.EstimatedTime);
        Assert.Equal(expectedCrew, order.RequiredEmployees);
    }

    [Fact]
    public void A_Client_That_Omits_The_Level_Books_At_Normal()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        CleansiaStartupBase.ConfigureJsonSerialization(options);
        const string basket =
            """{"selectedServiceIds":["s"],"selectedPackageIds":[],"rooms":1,"bathrooms":1,"planCode":"PLUS"}""";
        const string booking =
            """{"customerName":"Jane","customerEmail":"j@example.com","customerPhone":"1","selectedPackageIds":[],"selectedServiceIds":["s"],"rooms":1,"bathrooms":1,"extras":{},"cleaningDate":"2026-10-01T10:00:00Z","paymentType":2,"totalPrice":100}""";

        Assert.Equal(DirtinessLevel.Normal, JsonSerializer.Deserialize<QuoteOrder.Command>(basket, options)!.DirtinessLevel);
        Assert.Equal(DirtinessLevel.Normal, JsonSerializer.Deserialize<QuotePlusSavings.Query>(basket, options)!.DirtinessLevel);
        Assert.Equal(DirtinessLevel.Normal, JsonSerializer.Deserialize<CreateOrder.Command>(booking, options)!.DirtinessLevel);
        Assert.Equal(
            DirtinessLevel.Heavy,
            JsonSerializer.Deserialize<QuoteOrder.Command>(basket.Replace("}", ""","dirtinessLevel":2}"""), options)!.DirtinessLevel);
    }

    private static decimal Cents(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static DateTime CleaningDate(bool express) =>
        express ? DateTime.UtcNow.AddHours(3) : DateTime.UtcNow.AddDays(3);

    private Task<OrderPricingResult> PriceAsync(DirtinessLevel level, DateTime? cleaningDateUtc) =>
        CreateCalculator().CalculateAsync(
            [ServiceId], [], [ExtraSlug], rooms: 2, bathrooms: 1, level, currencyId: null,
            cleaningDateUtc, userId: null, nowUtc: DateTime.UtcNow, CancellationToken.None);

    private async Task<Order> CreateOrderAsync(
        DirtinessLevel level,
        bool express,
        string? userId,
        decimal servicePrice = ServicePrice,
        decimal? extraPrice = ExtraPrice)
    {
        var nowUtc = DateTime.UtcNow;
        var cleaningDate = express ? nowUtc.AddHours(3) : nowUtc.AddDays(3);
        string[] extras = extraPrice is null ? [] : [ExtraSlug];

        var pricing = await CreateCalculator(servicePrice, extraPrice).CalculateAsync(
            [ServiceId], [], extras, rooms: 2, bathrooms: 1, level, currencyId: null,
            cleaningDate, userId, nowUtc, CancellationToken.None);

        var factory = new OrderFactory(
            Mock.Of<IOrderRepository>(),
            ServiceRepository(),
            PackageRepository(),
            ExtraRepositoryDouble.Holding(_extra),
            CataloguePriceDoubles.Services(Czk, (ServiceId, servicePrice, 0m)),
            CataloguePriceDoubles.NoPackages(),
            ExtraPrices(extraPrice),
            PayConfigRepositoryDouble.CoveringServices(CreateOrderTestData.CurrencyId, ServiceId),
            Mock.Of<ICompanyInfoRepository>(),
            Mock.Of<ICountryConfigurationRepository>(),
            Mock.Of<IVatCalculator>(),
            _loyaltyService.Object,
            _memberships.Object,
            NoPreferredCleanerHold.Resolver,
            WorkContractResolvers.Resolver().Object,
            Mock.Of<INotificationProducer>(),
            Mock.Of<IAdminNotifier>(),
            NullLogger<OrderFactory>.Instance);

        return await factory.CreateAsync(
            new CreateOrderInput(
                UserId: userId,
                CustomerName: "Test Customer",
                CustomerEmail: "customer@example.com",
                CustomerPhone: "+420123456789",
                Address: AddressMockFactory.Generate(),
                Rooms: 2,
                Bathrooms: 1,
                SelectedExtraSlugs: extras,
                CleaningDate: cleaningDate,
                PaymentType: PaymentType.Card,
                Currency: Czk,
                SelectedServiceIds: [ServiceId],
                SelectedPackageIds: [],
                RawSubtotal: pricing.TotalPrice - pricing.ExpressSurchargeAmount,
                NowUtc: nowUtc,
                ReservedExpressWaiver: null,
                OperatorTenantId: null,
                DirtinessLevel: level),
            CancellationToken.None);
    }

    private async Task<QuoteOrder.Response> QuoteAsync(DirtinessLevel level, bool express, string? userId)
    {
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(userId);

        var handler = new QuoteOrder.Handler(
            CreateCalculator(),
            session.Object,
            _loyaltyService.Object,
            _memberships.Object,
            Mock.Of<ICreditAccountRepository>(),
            Mock.Of<ICurrencyResolutionService>());

        var result = await handler.Handle(
            new QuoteOrder.Command(
                [ServiceId], [], Rooms: 2, Bathrooms: 1, CurrencyId: null,
                SelectedExtraSlugs: [ExtraSlug], CleaningDate: CleaningDate(express), DirtinessLevel: level),
            CancellationToken.None);

        return result.Value;
    }

    private OrderPricingCalculator CreateCalculator(
        decimal servicePrice = ServicePrice, decimal? extraPrice = ExtraPrice) =>
        new(
            ServiceRepository(),
            PackageRepository(),
            ExtraRepositoryDouble.Holding(_extra),
            CataloguePriceDoubles.Services(Czk, (ServiceId, servicePrice, 0m)),
            CataloguePriceDoubles.NoPackages(),
            ExtraPrices(extraPrice),
            CataloguePriceDoubles.DefaultCurrency(Czk),
            ExpressWaiverMocks.NoWaiver().Object);

    private IExtraPriceRepository ExtraPrices(decimal? extraPrice) =>
        extraPrice is { } price
            ? CataloguePriceDoubles.Extras(Czk, (_extra.Id, price))
            : CataloguePriceDoubles.Extras(Czk);

    private IServiceRepository ServiceRepository()
    {
        var mock = new Mock<IServiceRepository>();
        mock.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { _service }.AsQueryable().BuildMock());
        return mock.Object;
    }

    private static IPackageRepository PackageRepository()
    {
        var mock = new Mock<IPackageRepository>();
        mock.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        return mock.Object;
    }
}
