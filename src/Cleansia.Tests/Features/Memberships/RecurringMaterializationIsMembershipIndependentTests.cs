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
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// Owner ruling 4 (2026-08-03): <b>a lapsed membership does not stop a recurring schedule — occurrences
/// keep generating, at full price.</b> The ruling says this is already true by construction, so this is a
/// CHARACTERIZATION test: it pins the construction so the express-waiver wiring cannot quietly change it.
///
/// <para>Two independent properties, and the second is the one the waiver could have broken:</para>
/// <list type="number">
/// <item>The sweep's template query carries no membership predicate at all, so a lapse cannot stop it.</item>
/// <item>Each occurrence is priced with <c>userId: null</c> and built with an explicitly null reservation,
/// so a LIVE membership cannot have this background job spend the member's monthly express waivers on
/// occurrences nobody asked to be express — and a lapsed one changes nothing, because the express axis
/// was never in play.</item>
/// </list>
/// </summary>
public class RecurringMaterializationIsMembershipIndependentTests
{
    private const string TemplateId = "template-recurring-1";
    private const string UserId = "user-recurring-1";
    private const string SavedAddressId = "saved-address-1";

    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderFactory> _orderFactory = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public RecurringMaterializationIsMembershipIndependentTests()
    {
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Currency.Create("CZK", "Kč", "Czech Koruna", 1m));
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = "order-recurring-1",
                UserId = UserId,
                PaymentType = PaymentType.Cash,
            }));

        var address = AddressMockFactory.Generate();
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        _savedAddressRepository
            .Setup(r => r.GetByIdAsync(SavedAddressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);
        _addressRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(address);

        var user = User.CreateWithPassword(
            "recurring@cleansia.test", "Password1!", "Rec", "Urring", UserProfile.Customer);
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

    private MaterializeRecurringBookings.Handler CreateHandler() =>
        new(
            _templateRepository.Object,
            _savedAddressRepository.Object,
            _addressRepository.Object,
            _currencyRepository.Object,
            _pricingCalculator.Object,
            _orderFactory.Object,
            _tenantProvider.Object,
            _unitOfWork.Object,
            NullLogger<MaterializeRecurringBookings.Handler>.Instance);

    [Fact]
    public async Task OccurrencesGenerateWithNoMembershipLookupAndNoWaiver()
    {
        var inputs = new List<CreateOrderInput>();
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => inputs.Add(input))
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = "order-recurring-1",
                UserId = UserId,
                PaymentType = PaymentType.Cash,
            }));

        var result = await CreateHandler().Handle(
            new MaterializeRecurringBookings.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(inputs);
        Assert.All(inputs, input => Assert.Null(input.ReservedExpressWaiver));

        // Priced as a guest on the express axis: no user, no cleaning date, so the pure resolver is asked
        // nothing it could answer "waived" to. The member's discount still applies downstream — the
        // factory resolves it from input.UserId — this is only the express surcharge.
        _pricingCalculator.Verify(
            c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), null, null, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
