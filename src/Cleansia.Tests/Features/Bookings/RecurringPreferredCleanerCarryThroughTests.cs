using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
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
/// <para>The pass-through is driven from the REAL writer — the template is built by
/// <c>RecurringBookingTemplate.Create</c>, and one case runs <c>CreateRecurringBooking.Handler</c> end to
/// end into the sweep — so breaking either half turns this class red. While the column had no producer
/// these tests wrote it by reflection, which pins a field but proves no path.</para>
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
    private readonly Mock<IOrderRepository> _orderRepository = new();
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
            SweepCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(_inputs);
        Assert.All(_inputs, input => Assert.Equal(PreferredEmployeeId, input.PreferredEmployeeId));
    }

    [Fact]
    public async Task A_Template_Without_A_Preference_Still_Materializes_With_None()
    {
        GivenTemplate(preferredEmployeeId: null);

        var result = await CreateHandler().Handle(
            SweepCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(_inputs);
        Assert.All(_inputs, input => Assert.Null(input.PreferredEmployeeId));
    }

    /// <summary>
    /// The customer's own command is the only writer of the template's preference, so the pass-through
    /// is exercised from it rather than from a hand-built entity: create the template through
    /// <see cref="CreateRecurringBooking.Handler"/>, then hand the row it produced to the sweep.
    /// </summary>
    [Fact]
    public async Task A_Template_Created_Through_The_Command_Materializes_Occurrences_Carrying_It()
    {
        GivenTemplate(await TemplateFromTheCreateCommandAsync(PreferredEmployeeId));

        var result = await CreateHandler().Handle(
            SweepCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(_inputs);
        Assert.All(_inputs, input => Assert.Equal(PreferredEmployeeId, input.PreferredEmployeeId));
    }

    private async Task<RecurringBookingTemplate> TemplateFromTheCreateCommandAsync(string preferredEmployeeId)
    {
        var address = AddressMockFactory.Generate();
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        SetPrivate(saved, nameof(SavedAddress.Address), address);

        var savedAddresses = new Mock<ISavedAddressRepository>();
        savedAddresses
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([saved]);

        var memberships = new Mock<IUserMembershipRepository>();
        memberships
            .Setup(r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembership.Create(
                userId: UserId,
                membershipPlanId: "plan-plus",
                stripeSubscriptionId: "sub_1",
                currentPeriodStart: DateTime.UtcNow.AddDays(-1),
                currentPeriodEnd: DateTime.UtcNow.AddMonths(1)));

        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(UserId);

        RecurringBookingTemplate? created = null;
        var templates = new Mock<IRecurringBookingTemplateRepository>();
        templates
            .Setup(r => r.Add(It.IsAny<RecurringBookingTemplate>()))
            .Callback((RecurringBookingTemplate t) => created = t);

        var result = await new CreateRecurringBooking.Handler(
                templates.Object, savedAddresses.Object, memberships.Object, session.Object)
            .Handle(
                new CreateRecurringBooking.Command(
                    Frequency: (int)RecurrenceFrequency.Weekly,
                    DayOfWeek: (int)DateTime.UtcNow.AddDays(2).DayOfWeek,
                    TimeOfDay: "10:00",
                    Rooms: 2,
                    Bathrooms: 1,
                    SavedAddressId: SavedAddressId,
                    SelectedServiceIds: [CreateOrderTestData.ServiceId],
                    SelectedPackageIds: [],
                    PaymentType: (int)PaymentType.Cash,
                    StartsOn: DateTime.UtcNow.AddDays(-7),
                    EndsOn: null,
                    PreferredEmployeeId: preferredEmployeeId),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        return created!;
    }

    private void GivenTemplate(string? preferredEmployeeId) =>
        GivenTemplate(RecurringBookingTemplate.Create(
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
            startsOn: DateTime.UtcNow.AddDays(-7),
            endsOn: null,
            preferredEmployeeId: preferredEmployeeId));

    private void GivenTemplate(RecurringBookingTemplate template)
    {
        var user = User.CreateWithPassword(
            "preferred@cleansia.test", "Password1!", "Pia", "Preferred", UserProfile.Customer);
        user.Id = UserId;

        template.Id = TemplateId;
        SetPrivate(template, nameof(RecurringBookingTemplate.User), user);

        _templateRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { template }.AsQueryable().BuildMock());
    }

    private static void SetPrivate<T>(T target, string property, object value) =>
        typeof(T).GetProperty(property)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(target, [value]);

    /// <summary>
    /// The pass-through lives in the PER-TEMPLATE atom, which is where the sweep now does all of its work
    /// — <c>MaterializeRecurringBookings</c> itself only selects ids and dispatches one of these per
    /// template in its own DI scope. Driving the atom directly is the same code path with the scope
    /// plumbing left to the tests that are actually about isolation.
    /// </summary>
    private MaterializeRecurringBookingTemplate.Handler CreateHandler()
    {
        // No order has been spawned for this template yet, so the materializer's duplicate guard finds
        // nothing and every candidate occurrence is created — which is the arrangement these tests want.
        _orderRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(Array.Empty<Cleansia.Core.Domain.Orders.Order>().AsQueryable().BuildMock());

        return new(
            _templateRepository.Object,
            _savedAddressRepository.Object,
            _addressRepository.Object,
            _currencyRepository.Object,
            _orderRepository.Object,
            _pricingCalculator.Object,
            _orderFactory.Object,
            _tenantProvider.Object,
            _unitOfWork.Object,
            NullLogger<MaterializeRecurringBookingTemplate.Handler>.Instance);
    }

    private static MaterializeRecurringBookingTemplate.Command SweepCommand() =>
        new(TemplateId, DateTime.UtcNow, HorizonDays: 7);
}
