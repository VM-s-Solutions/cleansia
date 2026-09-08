using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
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
/// Owner ruling 2026-09-08 (T-0690): <b>a recurring schedule is a Cleansia Plus benefit, so it stops
/// generating when the membership lapses.</b>
///
/// <para><b>This class previously asserted the opposite</b>, under owner ruling 4 of 2026-08-03 — "a
/// lapsed membership does not stop a recurring schedule; occurrences keep generating, at full price".
/// It was named <c>RecurringMaterializationIsMembershipIndependentTests</c> and it was right at the
/// time. The newer ruling is that no Plus benefit is granted without payment, and a schedule that
/// outlives the subscription is the largest thing the old position gave away: one paid month bought a
/// permanently re-specifiable scheduling engine.</para>
///
/// <para><b>The positive leg is the load-bearing one.</b> "Lapsed generates nothing" passes for any
/// reason the sweep produced nothing — a broken query, a bad horizon, an exception swallowed upstream.
/// What gives it meaning is <see cref="A_paid_member_still_gets_their_occurrences"/> proving the same
/// arrangement DOES generate once the membership is paid.</para>
///
/// <para>The express-waiver property is unchanged and still pinned: each occurrence is priced with
/// <c>userId: null</c> and an explicitly null reservation, so this background job cannot spend a
/// member's monthly waivers on occurrences nobody asked to be express.</para>
/// </summary>
public class RecurringMaterializationRequiresPaidMembershipTests
{
    private const string TemplateId = "template-recurring-1";
    private const string UserId = "user-recurring-1";
    private const string SavedAddressId = "saved-address-1";

    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IAddressRepository> _addressRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();
    private readonly Mock<IOrderFactory> _orderFactory = new();
    private readonly Mock<IUserMembershipRepository> _memberships = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public RecurringMaterializationRequiresPaidMembershipTests()
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

    /// <summary>
    /// Both properties live in the PER-TEMPLATE atom, which is where the sweep does all of its work —
    /// <c>MaterializeRecurringBookings</c> itself only selects ids and dispatches one of these per
    /// template in its own DI scope.
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
            _memberships.Object,
            _tenantProvider.Object,
            _unitOfWork.Object,
            NullLogger<MaterializeRecurringBookingTemplate.Handler>.Instance);
    }

    /// <summary>Arrange the owner as a paid member, which is what the entitlement read answers.</summary>
    private void ArrangePaidMembership()
    {
        var plan = MembershipPlan.Create(
            code: "PLUS_MONTHLY", name: "Plus", monthlyPriceCzk: 199m, stripePriceId: "price_plus",
            discountPercentage: 5m, freeCancellationWindowHours: 4, allowsExpressUpgrade: true);
        var membership = UserMembership.Create(
            UserId, plan.Id, "sub_test", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(20), null);

        _memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
    }

    /// <summary>
    /// The load-bearing positive leg. Same arrangement as the lapsed case in every respect except the
    /// membership, so the negative leg below cannot pass for an unrelated reason.
    /// </summary>
    [Fact]
    public async Task A_paid_member_still_gets_their_occurrences()
    {
        ArrangePaidMembership();
        var inputs = CaptureOrderInputs();

        var result = await CreateHandler().Handle(
            new MaterializeRecurringBookingTemplate.Command(TemplateId, DateTime.UtcNow, HorizonDays: 7),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(inputs);

        // Unchanged by T-0690: priced as a guest on the EXPRESS axis so this background job cannot spend
        // the member's monthly waivers on occurrences nobody asked to be express.
        Assert.All(inputs, input => Assert.Null(input.ReservedExpressWaiver));
        _pricingCalculator.Verify(
            c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), null, null, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// The ruling itself. A lapsed member — no entitled membership — gets nothing new generated.
    /// </summary>
    [Fact]
    public async Task A_lapsed_member_generates_nothing()
    {
        // The entitlement read answers null, which is what a lapsed, past-due, paused or cancelled
        // membership all produce. Deliberately not stubbed to a trialing row: there are no trialing rows
        // any more (T-0690 removed the trial and the admin commands refuse to set one).
        _memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var inputs = CaptureOrderInputs();

        var result = await CreateHandler().Handle(
            new MaterializeRecurringBookingTemplate.Command(TemplateId, DateTime.UtcNow, HorizonDays: 7),
            CancellationToken.None);

        // A successful no-op, not a failure: a lapse is an ordinary state of the world, and a sweep that
        // reported failure for every lapsed template would drown the real errors.
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.OrdersCreated);
        Assert.Empty(inputs);
    }

    /// <summary>
    /// A lapse is a PAUSE, not a deletion. The template is left exactly as authored so resubscribing
    /// resumes the schedule on the next tick with no action from the customer and no support ticket.
    /// Without this, a future "tidy up dead templates" change could quietly make the lapse permanent.
    /// </summary>
    [Fact]
    public async Task A_lapse_does_not_deactivate_or_mutate_the_template()
    {
        _memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        CaptureOrderInputs();

        await CreateHandler().Handle(
            new MaterializeRecurringBookingTemplate.Command(TemplateId, DateTime.UtcNow, HorizonDays: 7),
            CancellationToken.None);

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private List<CreateOrderInput> CaptureOrderInputs()
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
        return inputs;
    }
}
