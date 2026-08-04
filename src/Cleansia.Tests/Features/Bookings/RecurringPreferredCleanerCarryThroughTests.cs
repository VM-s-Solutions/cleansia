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

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// TC-PREF-RECUR-0 (ADR-0036 D8). The materializer used to pass a literal <c>null</c> here, so a
/// recurring customer's choice of cleaner was dropped on every occurrence the sweep generated — and
/// recurring is the strongest case for the perk, not the weakest: the lead time is ~7 days, which is
/// where the hold costs the board least.
///
/// <para>The template column has no command writing it yet, so this is what keeps the wiring alive
/// until that lands — a pass-through with no producer is exactly the thing a later reader deletes as
/// dead.</para>
///
/// <para>The sweep passes the preference UNFILTERED and lets the factory's resolver re-run the gates
/// per occurrence: it has no user session, and the alternative to degrading in the resolver is a second
/// copy of the membership and eligibility predicates in a 03:00 job where nobody can react to an
/// error.</para>
/// </summary>
public class RecurringPreferredCleanerCarryThroughTests
{
    private const string TemplateId = "template-preferred-1";
    private const string UserId = "user-preferred-1";
    private const string SavedAddressId = "saved-address-preferred-1";
    private const string PreferredEmployeeId = "employee-preferred-1";

    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderFactory> _orderFactory = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly List<CreateOrderInput> _inputs = [];

    public RecurringPreferredCleanerCarryThroughTests()
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
            .Callback((CreateOrderInput input, CancellationToken _) => _inputs.Add(input))
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = "order-preferred-1",
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
    }

    [Fact]
    public async Task Every_Occurrence_Carries_The_Templates_Preferred_Cleaner()
    {
        GivenTemplate(PreferredEmployeeId);

        var result = await CreateHandler().Handle(
            new MaterializeRecurringBookings.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(_inputs);
        Assert.All(_inputs, input => Assert.Equal(PreferredEmployeeId, input.PreferredEmployeeId));
    }

    [Fact]
    public async Task A_Template_Without_A_Preference_Still_Materializes_With_None()
    {
        GivenTemplate(preferredEmployeeId: null);

        var result = await CreateHandler().Handle(
            new MaterializeRecurringBookings.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(_inputs);
        Assert.All(_inputs, input => Assert.Null(input.PreferredEmployeeId));
    }

    private void GivenTemplate(string? preferredEmployeeId)
    {
        var user = User.CreateWithPassword(
            "preferred@cleansia.test", "Password1!", "Pia", "Preferred", UserProfile.Customer);
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
        SetPrivate(template, nameof(RecurringBookingTemplate.User), user);
        if (preferredEmployeeId is not null)
        {
            // Standing in for the recurring wizard's field, which is a separate ticket. Until it lands
            // the column has no production writer, and a pass-through nothing can exercise is a
            // pass-through that regresses unnoticed.
            SetPrivate(template, nameof(RecurringBookingTemplate.PreferredEmployeeId), preferredEmployeeId);
        }

        _templateRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { template }.AsQueryable().BuildMock());
    }

    private static void SetPrivate(RecurringBookingTemplate template, string property, object value) =>
        typeof(RecurringBookingTemplate)
            .GetProperty(property)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(template, [value]);

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
}
