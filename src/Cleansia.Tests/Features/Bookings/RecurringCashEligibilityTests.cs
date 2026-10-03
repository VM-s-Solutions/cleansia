using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Tests.Features.Orders;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// Owner ruling 2026-09-24 on the recurring entry points. A template always belongs to an account, so
/// only the crew term can fail: cash is refused when the selection needs more than one cleaner, by the
/// live catalogue's minutes through the same sum the order factory stamps. Rooms and bathrooms enter
/// that sum through a service's per-room minutes; every template here is two rooms and a bathroom.
/// </summary>
public class RecurringCashEligibilityTests
{
    private const string UserId = "user-recurring-cash";
    private const string TemplateId = "tpl-recurring-cash";

    private static readonly Service TwoHours = CatalogueDoubles.Service("svc-120", 120);
    private static readonly Service TwoHoursAndAMinute = CatalogueDoubles.Service("svc-121", 121);
    private static readonly Service OneHour = CatalogueDoubles.Service("svc-60", 60);
    private static readonly Service OneHourAndAMinute = CatalogueDoubles.Service("svc-61", 61);
    private static readonly Package PackageOf61 = CatalogueDoubles.Package("pkg-61", OneHourAndAMinute);
    private static readonly Service TwoHoursWithTheRooms = CatalogueDoubles.Service("svc-90-10", 90, minutesPerRoom: 10);
    private static readonly Service OverTwoHoursWithTheRooms = CatalogueDoubles.Service("svc-90-11", 90, minutesPerRoom: 11);

    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();

    public RecurringCashEligibilityTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _savedAddressRepository
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _templateRepository
            .Setup(r => r.GetByIdForOwnerAsync(TemplateId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Template(PaymentType.Cash, [TwoHoursAndAMinute.Id], []));
        _membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveMembership());
    }

    public static TheoryData<PaymentType, string[], string[], bool> Selections => new()
    {
        { PaymentType.Cash, ["svc-120"], [], true },
        { PaymentType.Cash, ["svc-121"], [], false },
        { PaymentType.Cash, ["svc-60"], ["pkg-61"], false },
        { PaymentType.Cash, ["svc-61", "svc-61"], [], true },
        { PaymentType.Cash, ["svc-90-10"], [], true },
        { PaymentType.Cash, ["svc-90-11"], [], false },
        { PaymentType.Card, ["svc-121"], [], true },
        { PaymentType.Card, ["svc-60"], ["pkg-61"], true },
    };

    [Theory]
    [MemberData(nameof(Selections))]
    public async Task Creating_A_Template_Refuses_Cash_Only_When_The_Selection_Needs_Two_Cleaners(
        PaymentType paymentType, string[] serviceIds, string[] packageIds, bool accepted)
    {
        var result = await CreateValidator().ValidateAsync(CreateCommand(paymentType, serviceIds, packageIds));

        AssertCashVerdict(result, accepted);
    }

    public static TheoryData<string, DirtinessLevel, bool> Levels => new()
    {
        { "svc-120", DirtinessLevel.Normal, true },
        { "svc-120", DirtinessLevel.Increased, false },
        { "svc-60", DirtinessLevel.Heavy, true },
        { "svc-90-10", DirtinessLevel.Heavy, false },
    };

    /// <summary>
    /// The level lengthens the booked time before the crew is counted, so a selection one cleaner covers
    /// at Normal can need two at a higher level, and cash falls away with it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Levels))]
    public async Task Creating_A_Template_Counts_The_Crew_At_Its_Level(string serviceId, DirtinessLevel level, bool accepted)
    {
        var result = await CreateValidator().ValidateAsync(
            CreateCommand(PaymentType.Cash, [serviceId], []) with { DirtinessLevel = level });

        AssertCashVerdict(result, accepted);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public async Task Updating_A_Template_Counts_The_Crew_At_Its_Level(string serviceId, DirtinessLevel level, bool accepted)
    {
        var result = await UpdateValidator().ValidateAsync(
            UpdateCommand(PaymentType.Cash, [serviceId], []) with { DirtinessLevel = level });

        AssertCashVerdict(result, accepted);
    }

    [Fact]
    public async Task An_Unknown_Level_Is_Refused_As_Such_Rather_Than_Failing_The_Cash_Check()
    {
        var create = await CreateValidator().ValidateAsync(
            CreateCommand(PaymentType.Cash, ["svc-120"], []) with { DirtinessLevel = (DirtinessLevel)7 });
        var update = await UpdateValidator().ValidateAsync(
            UpdateCommand(PaymentType.Cash, ["svc-120"], []) with { DirtinessLevel = (DirtinessLevel)7 });

        foreach (var result in new[] { create, update })
        {
            var error = Assert.Single(result.Errors);
            Assert.Equal(BusinessErrorMessage.InvalidEnumValue, error.ErrorMessage);
            Assert.Equal(nameof(CreateRecurringBooking.Command.DirtinessLevel), error.PropertyName);
        }
    }

    [Theory]
    [MemberData(nameof(Selections))]
    public async Task Updating_A_Template_Refuses_Cash_Only_When_The_Selection_Needs_Two_Cleaners(
        PaymentType paymentType, string[] serviceIds, string[] packageIds, bool accepted)
    {
        var result = await UpdateValidator().ValidateAsync(UpdateCommand(paymentType, serviceIds, packageIds));

        AssertCashVerdict(result, accepted);
    }

    /// <summary>
    /// The stored template is a legacy cash one needing two cleaners. Any edit that keeps cash on that
    /// selection is refused -- even one touching nothing but the time -- and the same edit switching to
    /// card, or to a one-cleaner selection, is accepted.
    /// </summary>
    [Theory]
    [InlineData(PaymentType.Cash, "svc-121", false)]
    [InlineData(PaymentType.Card, "svc-121", true)]
    [InlineData(PaymentType.Cash, "svc-120", true)]
    public async Task A_Legacy_Ineligible_Cash_Template_Is_Only_Editable_Onto_Card_Or_A_One_Cleaner_Selection(
        PaymentType paymentType, string serviceId, bool accepted)
    {
        var result = await UpdateValidator().ValidateAsync(
            UpdateCommand(paymentType, [serviceId], []) with { TimeOfDay = "11:30" });

        AssertCashVerdict(result, accepted);
    }

    [Fact]
    public async Task The_Recurring_List_Marks_Only_A_Cash_Template_Whose_Selection_Needs_Two_Cleaners()
    {
        var needsChange = Template(PaymentType.Cash, [TwoHoursAndAMinute.Id], [], "tpl-cash-121");
        var packaged = Template(PaymentType.Cash, [OneHour.Id], [PackageOf61.Id], "tpl-cash-package");
        var eligible = Template(PaymentType.Cash, [TwoHours.Id], [], "tpl-cash-120");
        var card = Template(PaymentType.Card, [TwoHoursAndAMinute.Id], [], "tpl-card-121");
        var paused = Template(PaymentType.Cash, [TwoHoursAndAMinute.Id], [], "tpl-paused").Pause();
        var roomsTipIt = Template(PaymentType.Cash, [OverTwoHoursWithTheRooms.Id], [], "tpl-cash-rooms");
        var levelTipsIt = Template(PaymentType.Cash, [TwoHours.Id], [], "tpl-cash-120-increased", DirtinessLevel.Increased);
        _templateRepository
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([needsChange, packaged, eligible, card, paused, roomsTipIt, levelTipsIt]);

        var result = await new GetMyRecurringBookings.Handler(
                _templateRepository.Object, _savedAddressRepository.Object, _session.Object, Services(), Packages(), Mock.Of<ICountryConfigurationRepository>())
            .Handle(new GetMyRecurringBookings.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var flags = result.Value!.ToDictionary(t => t.Id, t => t.RequiresPaymentMethodChange);
        Assert.True(flags["tpl-cash-121"]);
        Assert.True(flags["tpl-cash-package"]);
        Assert.False(flags["tpl-cash-120"]);
        Assert.False(flags["tpl-card-121"]);
        Assert.True(flags["tpl-paused"]);
        Assert.True(flags["tpl-cash-rooms"]);
        Assert.True(flags["tpl-cash-120-increased"]);
    }

    [Fact]
    public async Task A_List_Of_Card_Templates_Reads_No_Catalogue()
    {
        var services = new Mock<IServiceRepository>(MockBehavior.Strict);
        var packages = new Mock<IPackageRepository>(MockBehavior.Strict);
        _templateRepository
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Template(PaymentType.Card, [TwoHoursAndAMinute.Id], [])]);

        var result = await new GetMyRecurringBookings.Handler(
                _templateRepository.Object, _savedAddressRepository.Object, _session.Object, services.Object, packages.Object, Mock.Of<ICountryConfigurationRepository>())
            .Handle(new GetMyRecurringBookings.Query(), CancellationToken.None);

        Assert.False(Assert.Single(result.Value!).RequiresPaymentMethodChange);
    }

    private static void AssertCashVerdict(FluentValidation.Results.ValidationResult result, bool accepted)
    {
        if (accepted)
        {
            Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
            return;
        }

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.OrderCashNotAvailable, error.ErrorMessage);
        Assert.Equal(nameof(CreateRecurringBooking.Command.PaymentType), error.PropertyName);
    }

    private static IServiceRepository Services() =>
        CatalogueDoubles.Services(
            TwoHours, TwoHoursAndAMinute, OneHour, OneHourAndAMinute, TwoHoursWithTheRooms, OverTwoHoursWithTheRooms);

    private static IPackageRepository Packages() => CatalogueDoubles.Packages(PackageOf61);

    private CreateRecurringBooking.Validator CreateValidator() =>
        new(
            Mock.Of<IOrderRepository>(),
            _session.Object,
            _savedAddressRepository.Object,
            OrderMarketDoubles.Trading(CreateOrderTestData.DefaultCurrency()),
            OrderMarketDoubles.Servicing("country-cz"),
            Services(),
            Packages(),
            Cleansia.Tests.Features.Legal.CustomerConsentDoubles.Consented(),
            Mock.Of<Cleansia.Core.AppServices.Services.Interfaces.ILegalDocumentResolver>(),
            SavedCards.SavedCardDoubles.Guaranteed(), Mock.Of<IReceivableRepository>());

    private UpdateRecurringBooking.Validator UpdateValidator() =>
        new(
            _templateRepository.Object,
            _membershipRepository.Object,
            _session.Object,
            Mock.Of<IOrderRepository>(),
            _savedAddressRepository.Object,
            OrderMarketDoubles.Trading(CreateOrderTestData.DefaultCurrency()),
            OrderMarketDoubles.Servicing("country-cz"),
            Services(),
            Packages(),
            SavedCards.SavedCardDoubles.Guaranteed(), Mock.Of<IReceivableRepository>());

    private static CreateRecurringBooking.Command CreateCommand(
        PaymentType paymentType, IReadOnlyList<string> serviceIds, IReadOnlyList<string> packageIds) =>
        new(
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: "09:00",
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: "saved-1",
            SelectedServiceIds: serviceIds,
            SelectedPackageIds: packageIds,
            PaymentType: (int)paymentType,
            StartsOn: DateTime.UtcNow.AddDays(3),
            EarlyPerformanceRequested: true);

    private static UpdateRecurringBooking.Command UpdateCommand(
        PaymentType paymentType, IReadOnlyList<string> serviceIds, IReadOnlyList<string> packageIds) =>
        new(
            TemplateId: TemplateId,
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: "09:00",
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: "saved-1",
            SelectedServiceIds: serviceIds,
            SelectedPackageIds: packageIds,
            PaymentType: (int)paymentType,
            StartsOn: DateTime.UtcNow.AddDays(3));

    private static RecurringBookingTemplate Template(
        PaymentType paymentType, IReadOnlyList<string> serviceIds, IReadOnlyList<string> packageIds,
        string id = TemplateId, DirtinessLevel dirtinessLevel = DirtinessLevel.Normal)
    {
        var template = RecurringBookingTemplate.Create(
            userId: UserId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: System.DayOfWeek.Tuesday,
            timeOfDay: new TimeOnly(9, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: "saved-1",
            selectedServiceIds: serviceIds,
            selectedPackageIds: packageIds,
            paymentType: paymentType,
            startsOn: DateTime.UtcNow.AddDays(1),
            dirtinessLevel: dirtinessLevel);
        template.Id = id;
        return template;
    }

    private static UserMembership ActiveMembership()
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            discountPercentage: 10m,
            allowsExpressUpgrade: true);
        return UserMembership.Create(
            userId: UserId,
            membershipPlanId: plan.Id,
            currencyId: "currency-czk",
            stripeSubscriptionId: "sub_1",
            currentPeriodStart: DateTime.UtcNow.AddDays(-1),
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
    }
}
