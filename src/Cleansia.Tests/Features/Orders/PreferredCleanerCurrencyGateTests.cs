using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A preferred cleaner must be paid in the currency the order is priced in. A cleaner is paid in the
/// currency of the country they work in (owner ruling 2026-09-12); the board hides and take refuses a
/// job in any other currency, so a hold granted across currencies could only lapse -- the push sent,
/// the seat withheld for the whole hold, and the cleaner unable to act on it. The four places the
/// eligibility rule lives all carry the currency as a second conjunct of the SAME rule, under the same
/// key, with the completed-order term still judged first.
/// </summary>
public class PreferredCleanerCurrencyGateTests
{
    private const string UserId = "user-plus";
    private const string OrderId = "order-1";
    private const string TemplateId = "template-1";
    private const string SavedAddressId = "saved-address-1";
    private const string Czechia = "cz";
    private const string CzkCleanerId = "employee-czk";
    private const string EurCleanerId = "employee-eur";
    private const string StrangerId = "employee-stranger";

    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();
    private static readonly Currency Eur = WithId(Currency.Create("EUR", "€", "Euro"), "currency-eur");

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IOrderPricingCalculator> _pricingCalculator = new();

    /// <summary>Czechia trades in CZK; one cleaner is paid in CZK, the other in EUR.</summary>
    private readonly ICurrencyResolutionService _markets = OrderMarketDoubles.TradingAndPaying(
        Czk, [(Czechia, Czk)], (CzkCleanerId, Czk), (EurCleanerId, Eur));

    public PreferredCleanerCurrencyGateTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveMembership());
        foreach (var served in new[] { CzkCleanerId, EurCleanerId })
        {
            _orderRepository
                .Setup(r => r.UserHasCompletedOrderWithEmployeeAsync(UserId, served, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }
        _orderRepository
            .Setup(r => r.UserHasCompletedOrderWithEmployeeAsync(UserId, StrangerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderRepository
            .Setup(r => r.GetOwnerAndCurrencyAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderOwnerAndCurrency(UserId, Czk.Id));
        _savedAddressRepository
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CzechSavedAddress()]);
        _templateRepository
            .Setup(r => r.ExistsAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepository
            .Setup(r => r.GetByIdAsync(TemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Template());

        _serviceRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        _packageRepository
            .Setup(r => r.ExistWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        _currencyRepository
            .Setup(r => r.IsOfferableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _pricingCalculator
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
    }

    // ---------------------------------------------------------------- CreateOrder

    [Fact]
    public async Task CreateOrder_Accepts_A_Cleaner_Paid_In_The_Orders_Currency()
    {
        var result = await CreateOrderValidator().ValidateAsync(
            CreateOrderTestData.ValidCommand(preferredEmployeeId: CzkCleanerId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task CreateOrder_Refuses_A_Cleaner_Paid_In_Another_Currency()
    {
        var result = await CreateOrderValidator().ValidateAsync(
            CreateOrderTestData.ValidCommand(preferredEmployeeId: EurCleanerId));

        AssertNotEligible(result, nameof(CreateOrder.Command.PreferredEmployeeId));
    }

    [Fact]
    public async Task CreateOrder_Judges_The_Completed_Order_Before_The_Currency()
    {
        var result = await CreateOrderValidator().ValidateAsync(
            CreateOrderTestData.ValidCommand(preferredEmployeeId: StrangerId));

        AssertNotEligible(result, nameof(CreateOrder.Command.PreferredEmployeeId));
        VerifyNoCleanerCurrencyResolved();
    }

    // ---------------------------------------------------------------- ChoosePreferredCleaner

    [Fact]
    public async Task ChoosePreferredCleaner_Accepts_A_Cleaner_Paid_In_The_Orders_Currency()
    {
        var result = await ChooseValidator().ValidateAsync(new ChoosePreferredCleaner.Command(OrderId, CzkCleanerId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task ChoosePreferredCleaner_Refuses_A_Cleaner_Paid_In_Another_Currency()
    {
        var result = await ChooseValidator().ValidateAsync(new ChoosePreferredCleaner.Command(OrderId, EurCleanerId));

        AssertNotEligible(result, nameof(ChoosePreferredCleaner.Command.EmployeeId));
    }

    [Fact]
    public async Task ChoosePreferredCleaner_Judges_The_Completed_Order_Before_The_Currency()
    {
        var result = await ChooseValidator().ValidateAsync(new ChoosePreferredCleaner.Command(OrderId, StrangerId));

        AssertNotEligible(result, nameof(ChoosePreferredCleaner.Command.EmployeeId));
        VerifyNoCleanerCurrencyResolved();
    }

    /// <summary>Someone else's order passes the currency term untouched; the handler's not-found is the only answer.</summary>
    [Fact]
    public async Task ChoosePreferredCleaner_Does_Not_Judge_The_Currency_Of_Another_Customers_Order()
    {
        _orderRepository
            .Setup(r => r.GetOwnerAndCurrencyAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderOwnerAndCurrency("user-someone-else", Czk.Id));

        var result = await ChooseValidator().ValidateAsync(new ChoosePreferredCleaner.Command(OrderId, EurCleanerId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    /// <summary>
    /// The term reads two columns, not the order graph: the handler loads the order itself, so a
    /// full load here was the same order materialised twice per request.
    /// </summary>
    [Fact]
    public async Task ChoosePreferredCleaner_Reads_The_Orders_Owner_And_Currency_Without_Loading_It()
    {
        await ChooseValidator().ValidateAsync(new ChoosePreferredCleaner.Command(OrderId, EurCleanerId));

        _orderRepository.Verify(r => r.GetOwnerAndCurrencyAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _orderRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _orderRepository.Verify(r => r.GetQueryable(), Times.Never);
    }

    // ---------------------------------------------------------------- CreateRecurringBooking

    [Fact]
    public async Task CreateRecurringBooking_Accepts_A_Cleaner_Paid_In_The_Address_Currency()
    {
        var result = await CreateRecurringValidator().ValidateAsync(CreateRecurringCommand(CzkCleanerId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task CreateRecurringBooking_Refuses_A_Cleaner_Paid_In_Another_Currency()
    {
        var result = await CreateRecurringValidator().ValidateAsync(CreateRecurringCommand(EurCleanerId));

        AssertNotEligible(result, nameof(CreateRecurringBooking.Command.PreferredEmployeeId));
    }

    [Fact]
    public async Task CreateRecurringBooking_Judges_The_Completed_Order_Before_The_Currency()
    {
        var result = await CreateRecurringValidator().ValidateAsync(CreateRecurringCommand(StrangerId));

        AssertNotEligible(result, nameof(CreateRecurringBooking.Command.PreferredEmployeeId));
        VerifyNoCleanerCurrencyResolved();
    }

    // ---------------------------------------------------------------- UpdateRecurringBooking

    [Fact]
    public async Task UpdateRecurringBooking_Accepts_A_Cleaner_Paid_In_The_Address_Currency()
    {
        var result = await UpdateRecurringValidator().ValidateAsync(UpdateRecurringCommand(CzkCleanerId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task UpdateRecurringBooking_Refuses_A_Cleaner_Paid_In_Another_Currency()
    {
        var result = await UpdateRecurringValidator().ValidateAsync(UpdateRecurringCommand(EurCleanerId));

        AssertNotEligible(result, nameof(UpdateRecurringBooking.Command.PreferredEmployeeId));
    }

    [Fact]
    public async Task UpdateRecurringBooking_Judges_The_Completed_Order_Before_The_Currency()
    {
        var result = await UpdateRecurringValidator().ValidateAsync(UpdateRecurringCommand(StrangerId));

        AssertNotEligible(result, nameof(UpdateRecurringBooking.Command.PreferredEmployeeId));
        VerifyNoCleanerCurrencyResolved();
    }

    // ---------------------------------------------------------------- fixtures

    private static void AssertNotEligible(FluentValidation.Results.ValidationResult result, string property)
    {
        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors);
        Assert.Equal(property, failure.PropertyName);
        Assert.Equal(BusinessErrorMessage.PreferredEmployeeNotEligible, failure.ErrorMessage);
    }

    private void VerifyNoCleanerCurrencyResolved() =>
        Mock.Get(_markets).Verify(
            s => s.ResolveCurrencyForEmployeeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private CreateOrder.Validator CreateOrderValidator() =>
        new(
            _packageRepository.Object,
            _serviceRepository.Object,
            _pricingCalculator.Object,
            _orderRepository.Object,
            _membershipRepository.Object,
            _session.Object,
            PayConfigRepositoryDouble.Holding(),
            _currencyRepository.Object,
            OrderMarketDoubles.AddressAsGiven(),
            _markets,
            CataloguePriceDoubles.Services(Czk, (CreateOrderTestData.ServiceId, 500m, 100m)),
            CataloguePriceDoubles.Packages(Czk, (CreateOrderTestData.PackageId, 1000m)),
            new Mock<IPromoCodeService>().Object);

    private ChoosePreferredCleaner.Validator ChooseValidator() =>
        new(_session.Object, _membershipRepository.Object, _orderRepository.Object, _markets);

    private CreateRecurringBooking.Validator CreateRecurringValidator() =>
        new(_orderRepository.Object, _session.Object, _savedAddressRepository.Object, _markets);

    private UpdateRecurringBooking.Validator UpdateRecurringValidator() =>
        new(
            _templateRepository.Object,
            _membershipRepository.Object,
            _session.Object,
            _orderRepository.Object,
            _savedAddressRepository.Object,
            _markets);

    private static CreateRecurringBooking.Command CreateRecurringCommand(string preferredEmployeeId) =>
        new(
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: "09:00",
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: SavedAddressId,
            SelectedServiceIds: [CreateOrderTestData.ServiceId],
            SelectedPackageIds: [],
            PaymentType: (int)PaymentType.Card,
            StartsOn: DateTime.UtcNow.AddDays(3),
            EndsOn: null,
            PreferredEmployeeId: preferredEmployeeId);

    private static UpdateRecurringBooking.Command UpdateRecurringCommand(string preferredEmployeeId) =>
        new(
            TemplateId: TemplateId,
            Frequency: (int)RecurrenceFrequency.Weekly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: "09:00",
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: SavedAddressId,
            SelectedServiceIds: [CreateOrderTestData.ServiceId],
            SelectedPackageIds: [],
            PaymentType: (int)PaymentType.Card,
            StartsOn: DateTime.UtcNow.AddDays(3),
            EndsOn: null,
            PreferredEmployeeId: preferredEmployeeId);

    private static SavedAddress CzechSavedAddress()
    {
        var address = Address.Create("Dlouhá 12", "Praha", "11000", Czechia);
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        typeof(SavedAddress).GetProperty(nameof(SavedAddress.Address))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(saved, [address]);
        return saved;
    }

    private static RecurringBookingTemplate Template() =>
        RecurringBookingTemplate.Create(
            userId: UserId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: System.DayOfWeek.Tuesday,
            timeOfDay: new TimeOnly(9, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: SavedAddressId,
            selectedServiceIds: [CreateOrderTestData.ServiceId],
            selectedPackageIds: [],
            paymentType: PaymentType.Card,
            startsOn: DateTime.UtcNow.AddDays(3));

    private static UserMembership ActiveMembership()
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            discountPercentage: 10m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true);

        return UserMembership.Create(
            userId: UserId,
            membershipPlanId: plan.Id,
            currencyId: "currency-czk",
            stripeSubscriptionId: "sub_1",
            currentPeriodStart: DateTime.UtcNow.AddDays(-1),
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
    }

    private static Currency WithId(Currency currency, string id)
    {
        currency.Id = id;
        currency.IsActive = true;
        return currency;
    }
}
