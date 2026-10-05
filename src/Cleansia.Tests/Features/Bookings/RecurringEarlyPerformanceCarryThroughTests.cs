using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Features.Orders;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// Decision 61: one early-performance request covers the series. The schedule records it once, and the
/// sweep copies it onto every occurrence it creates, so each order carries the act that covers it and
/// keeps it after the schedule is deleted. A schedule set up before the act existed creates orders with
/// none.
/// </summary>
public class RecurringEarlyPerformanceCarryThroughTests
{
    private const string TemplateId = "template-early-performance-1";
    private const string UserId = "user-early-performance-1";
    private const string SavedAddressId = "saved-address-early-performance-1";

    private static readonly DateTimeOffset ConsentedOn = new(2026, 9, 29, 8, 30, 0, TimeSpan.Zero);

    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderFactory> _orderFactory = new();
    private readonly Mock<IUserMembershipRepository> _memberships = new();
    private readonly List<Order> _created = [];

    public RecurringEarlyPerformanceCarryThroughTests()
    {
        _memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => UserMembershipMockFactory.Paid(userId));

        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<DirtinessLevel>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());

        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateOrderInput input, CancellationToken _) =>
            {
                var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
                {
                    Id = $"order-early-performance-{_created.Count + 1}",
                    UserId = UserId,
                    PaymentType = PaymentType.Cash,
                });
                _created.Add(order);
                return order;
            });

        var address = AddressMockFactory.Generate();
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        _savedAddressRepository
            .Setup(r => r.GetByIdAsync(SavedAddressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);
        _addressRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);
        _orderRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(Array.Empty<Order>().AsQueryable().BuildMock());
    }

    [Fact]
    public async Task Every_Occurrence_Carries_The_Schedules_Early_Performance_Request()
    {
        var template = NewTemplate();
        template.RecordEarlyPerformanceConsent(
            Order.EarlyPerformanceConsentTextVersionInForce, ConsentedOn, "cleansia.customer", "203.0.113.9", "iPhone 15");
        GivenTemplate(template);

        var result = await Sweep().Handle(SweepCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(_created);
        Assert.All(_created, order =>
        {
            Assert.Equal(Order.EarlyPerformanceConsentTextVersionInForce, order.EarlyPerformanceConsentTextVersion);
            Assert.Equal(ConsentedOn, order.EarlyPerformanceConsentedOn);
            Assert.Equal("cleansia.customer", order.EarlyPerformanceConsentClient);
            Assert.Equal("203.0.113.9", order.EarlyPerformanceConsentIpAddress);
            Assert.Equal("iPhone 15", order.EarlyPerformanceConsentDeviceLabel);
        });
    }

    [Fact]
    public async Task A_Schedule_Without_The_Request_Creates_Occurrences_Without_One()
    {
        GivenTemplate(NewTemplate());

        var result = await Sweep().Handle(SweepCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(_created);
        Assert.All(_created, order => Assert.Null(order.EarlyPerformanceConsentedOn));
    }

    private static RecurringBookingTemplate NewTemplate() =>
        RecurringBookingTemplate.Create(
            userId: UserId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: DateTime.UtcNow.AddDays(2).DayOfWeek,
            timeOfDay: new TimeOnly(10, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: SavedAddressId,
            selectedServiceIds: [CreateOrderTestData.ServiceId],
            selectedPackageIds: [],
            paymentType: PaymentType.Cash,
            startsOn: DateTime.UtcNow.AddDays(-7));

    private void GivenTemplate(RecurringBookingTemplate template)
    {
        var user = User.CreateWithPassword(
            "early-performance@cleansia.test", "Password1!", "Eva", "Early", UserProfile.Customer);
        user.Id = UserId;

        template.Id = TemplateId;
        typeof(RecurringBookingTemplate).GetProperty(nameof(RecurringBookingTemplate.User))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(template, [user]);

        _templateRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { template }.AsQueryable().BuildMock());
    }

    private MaterializeRecurringBookingTemplate.Handler Sweep() =>
        new(
            _templateRepository.Object,
            _savedAddressRepository.Object,
            _addressRepository.Object,
            Mock.Of<ICountryConfigurationRepository>(),
            OrderMarketDoubles.Trading(Currency.Create("CZK", "Kč", "Czech Koruna")),
            _orderRepository.Object,
            _pricingCalculator.Object,
            _orderFactory.Object,
            _memberships.Object,
            Mock.Of<IReceivableRepository>(),
            OrderMarketDoubles.OperatedBy("cleansia-cz"),
            Mock.Of<ITenantProvider>(),
            Mock.Of<IUnitOfWork>(),
            NullLogger<MaterializeRecurringBookingTemplate.Handler>.Instance,
            Mock.Of<INotificationProducer>());

    private static MaterializeRecurringBookingTemplate.Command SweepCommand() =>
        new(TemplateId, DateTime.UtcNow, HorizonDays: 7);
}
