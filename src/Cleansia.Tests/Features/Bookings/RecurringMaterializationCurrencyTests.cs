using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
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
/// A RECURRING OCCURRENCE IS PRICED IN THE CURRENCY OF THE COUNTRY ITS SAVED ADDRESS IS IN — the same
/// rule <c>CreateOrder</c> stamps a one-off booking with (owner ruling 2026-09-12). The template carries
/// no currency and needs none: it carries the address, and the address has a country. Before this the
/// materializer took the platform default unconditionally, so a Slovak schedule would have produced CZK
/// orders the day SK was switched on.
/// </summary>
public class RecurringMaterializationCurrencyTests
{
    private const string TemplateId = "template-currency-1";
    private const string UserId = "user-currency-1";
    private const string SavedAddressId = "saved-address-currency-1";
    private const string Slovakia = "country-svk";

    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();
    private static readonly Currency Eur = Euro();

    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderFactory> _orderFactory = new();
    private readonly Mock<IUserMembershipRepository> _memberships = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly List<CreateOrderInput> _inputs = [];

    public RecurringMaterializationCurrencyTests()
    {
        _memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => UserMembershipMockFactory.Paid(userId));
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => _inputs.Add(input))
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = "order-currency-1",
                UserId = UserId,
                PaymentType = PaymentType.Cash,
            }));
        _orderRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(Array.Empty<Cleansia.Core.Domain.Orders.Order>().AsQueryable().BuildMock());
    }

    private static Currency Euro()
    {
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = "currency-eur";
        eur.IsActive = true;
        return eur;
    }

    private void ArrangeTemplateAt(string countryId)
    {
        var address = AddressMockFactory.Generate(new AddressMockFactory.AddressPartial { CountryId = countryId });
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        _savedAddressRepository
            .Setup(r => r.GetByIdAsync(SavedAddressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);
        _addressRepository
            .Setup(r => r.GetByIdAsync(address.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var user = User.CreateWithPassword(
            "currency@cleansia.test", "Password1!", "Cur", "Rency", UserProfile.Customer);
        user.Id = UserId;

        var template = RecurringBookingTemplate.Create(
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
        template.Id = TemplateId;
        typeof(RecurringBookingTemplate).GetProperty(nameof(RecurringBookingTemplate.User))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(template, [user]);

        _templateRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { template }.AsQueryable().BuildMock());
    }

    private MaterializeRecurringBookingTemplate.Handler CreateHandler() =>
        new(
            _templateRepository.Object,
            _savedAddressRepository.Object,
            _addressRepository.Object,
            OrderMarketDoubles.Trading(Czk, (Slovakia, Eur)),
            _orderRepository.Object,
            _pricingCalculator.Object,
            _orderFactory.Object,
            _memberships.Object,
            _tenantProvider.Object,
            _unitOfWork.Object,
            NullLogger<MaterializeRecurringBookingTemplate.Handler>.Instance);

    private static MaterializeRecurringBookingTemplate.Command SweepCommand() =>
        new(TemplateId, DateTime.UtcNow, HorizonDays: 7);

    [Fact]
    public async Task A_Template_On_A_Slovak_Address_Materialises_Euro_Orders()
    {
        ArrangeTemplateAt(Slovakia);

        var result = await CreateHandler().Handle(SweepCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.OrdersCreated > 0, "the arrangement must produce at least one occurrence");
        Assert.All(_inputs, input => Assert.Same(Eur, input.Currency));
        _pricingCalculator.Verify(c => c.CalculateAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
            Eur.Id, It.IsAny<DateTime?>(), It.IsAny<string?>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Anti-vacuity: a Czech address keeps producing platform-default orders through the same path.</summary>
    [Fact]
    public async Task A_Template_On_A_Czech_Address_Materialises_Default_Currency_Orders()
    {
        ArrangeTemplateAt("country-cze");

        var result = await CreateHandler().Handle(SweepCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.OrdersCreated > 0);
        Assert.All(_inputs, input => Assert.Same(Czk, input.Currency));
    }
}
