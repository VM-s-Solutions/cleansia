using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Microsoft.Extensions.Logging;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The factory tells the operator's administrators of an order only when it is OFFERABLE the moment
/// it exists — a cash one-off. An unpaid card order is an abandoned checkout until the webhook says
/// otherwise and a recurring occurrence waits for the customer's confirm, so neither is announced
/// here. The event carries the operator the market resolved, the number, the money with its symbol,
/// the tender, the market and the id — and never the customer. A market with no operator logs a
/// warning and tells nobody rather than failing the booking.
/// </summary>
public sealed class OrderFactoryTellsAdministratorsTests
{
    private const string UserId = "user-factory-tells";
    private const string OperatorTenantId = "company-of-the-market";
    private const string CountryId = "country-cz-factory-tells";
    private static readonly DateTime Now = new(2026, 9, 19, 9, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly List<AdminEvent> _raised = [];
    private readonly List<(LogLevel Level, string Message)> _log = [];

    public OrderFactoryTellsAdministratorsTests()
    {
        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        _loyaltyService
            .Setup(s => s.ResolveTierDiscountForOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierDiscountResult(0m, null));
        _userMembershipRepository
            .Setup(r => r.GetEntitledForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Core.Domain.Memberships.UserMembership?)null);
        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((e, _) => _raised.Add(e))
            .Returns(Task.CompletedTask);
    }

    private OrderFactory Factory() => new(
        _orderRepository.Object,
        _serviceRepository.Object,
        _packageRepository.Object,
        ExtraRepositoryDouble.Empty(),
        CataloguePriceDoubles.NoServices(),
        CataloguePriceDoubles.NoPackages(),
        CataloguePriceDoubles.NoExtras(),
        PayConfigRepositoryDouble.Holding(),
        new Mock<ICompanyInfoRepository>().Object,
        new Mock<ICountryConfigurationRepository>().Object,
        new Mock<IVatCalculator>().Object,
        _loyaltyService.Object,
        _userMembershipRepository.Object,
        NoPreferredCleanerHold.Resolver,
        new Mock<INotificationProducer>().Object,
        _adminNotifier.Object,
        new CapturingLogger(_log));

    private Task<Order> CreateAsync(PaymentType paymentType, string? operatorTenantId = OperatorTenantId, string? recurringTemplateId = null) =>
        Factory().CreateAsync(
            new CreateOrderInput(
                UserId: UserId,
                CustomerName: "Jana Nováková",
                CustomerEmail: "jana.novakova@example.com",
                CustomerPhone: "+420777123456",
                Address: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
                Rooms: 2,
                Bathrooms: 1,
                SelectedExtraSlugs: [],
                CleaningDate: Now.AddDays(3),
                PaymentType: paymentType,
                Currency: Currency.Create("CZK", "Kč", "Czech koruna"),
                SelectedServiceIds: ["service-1"],
                SelectedPackageIds: [],
                RawSubtotal: 1250.5m,
                NowUtc: Now,
                ReservedExpressWaiver: null,
                OperatorTenantId: operatorTenantId,
                RecurringTemplateId: recurringTemplateId),
            CancellationToken.None);

    [Fact]
    public async Task A_Cash_One_Off_Tells_The_Operators_Administrators_At_Creation_With_The_Declared_Args()
    {
        var order = await CreateAsync(PaymentType.Cash);

        var raised = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.OrderNew, raised.Key);
        Assert.Equal(OperatorTenantId, raised.TenantId);
        Assert.Equal(order.Id, raised.Subject);
        var declared = AdminEventCatalog.Find(AdminNotificationEventCatalog.OrderNew).EmailArgOrder;
        Assert.Equal(declared.OrderBy(a => a), raised.Args.Keys.OrderBy(a => a));
        Assert.Equal(order.DisplayOrderNumber, raised.Args["orderNumber"]);
        Assert.Equal("1250.5 Kč", raised.Args["amount"]);
        Assert.Equal(nameof(PaymentType.Cash), raised.Args["paymentType"]);
        Assert.Equal(CountryId, raised.Args["countryId"]);
        Assert.Equal(order.Id, raised.Args["orderId"]);
        Assert.All(raised.Args.Values, value =>
        {
            Assert.DoesNotContain("Jana", value);
            Assert.DoesNotContain("@", value);
            Assert.DoesNotContain("+420", value);
        });
    }

    [Fact]
    public async Task An_Unpaid_Card_Order_Tells_Nobody_At_Creation()
    {
        await CreateAsync(PaymentType.Card);

        Assert.Empty(_raised);
    }

    [Fact]
    public async Task A_Recurring_Cash_Occurrence_Tells_Nobody_At_Creation()
    {
        await CreateAsync(PaymentType.Cash, recurringTemplateId: "tmpl-weekly");

        Assert.Empty(_raised);
    }

    [Fact]
    public async Task A_Market_With_No_Operator_Tells_Nobody_And_Warns_Without_Failing_The_Booking()
    {
        var order = await CreateAsync(PaymentType.Cash, operatorTenantId: null);

        Assert.NotNull(order);
        Assert.Empty(_raised);
        var warning = Assert.Single(_log, e => e.Level == LogLevel.Warning);
        Assert.Contains(order.Id, warning.Message);
    }

    private sealed class CapturingLogger(List<(LogLevel Level, string Message)> entries) : ILogger<OrderFactory>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add((logLevel, formatter(state, exception)));
    }
}
