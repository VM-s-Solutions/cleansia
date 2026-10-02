using System.Linq.Expressions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Tests.Common;
using Cleansia.Tests.Features.Bookings;
using Cleansia.Tests.Features.SavedCards;
using FluentValidation.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28 on the recurring doors to cash: a schedule written as cash, and a cash
/// occurrence confirmed, need a usable card saved in the currency the occurrences are priced in, the
/// customer holds at most two cash bookings that are open and not yet paid, and owes the company no open
/// receivable. The one-off booking is held to the same rules through the real pipeline in the integration
/// suite.
/// </summary>
public sealed class CustomerCashStandingTests
{
    private const string UserId = "user-cash-standing";
    private const string CountryId = "country-cz";
    private const string SavedAddressId = "saved-cash-standing";
    private const string TemplateId = "tpl-cash-standing";
    private const string OccurrenceId = "order-cash-standing";

    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();
    private static readonly Service TwoHours = CatalogueDoubles.Service("svc-120", 120);

    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ISavedAddressRepository> _savedAddresses = new();
    private readonly Mock<IRecurringBookingTemplateRepository> _templates = new();
    private readonly Mock<IUserMembershipRepository> _memberships = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly List<Order> _customerOrders = [];
    private ISavedCardRepository _savedCards = SavedCardDoubles.Guaranteed();

    public CustomerCashStandingTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _savedAddresses
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([SavedAddressInCzechia()]);
        _templates
            .Setup(r => r.GetByIdForOwnerAsync(TemplateId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Template());
        _memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveMembership());
        _orders
            .Setup(r => r.GetCountForOwnerAsync(UserId, It.IsAny<Expression<Func<Order, bool>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, Expression<Func<Order, bool>>? filter, CancellationToken _) =>
                _customerOrders.AsQueryable().Count(filter!));
    }

    public enum Cards { None, ExpiredLastMonth, OtherCurrencyOnly }

    [Theory]
    [InlineData(Cards.None)]
    [InlineData(Cards.ExpiredLastMonth)]
    [InlineData(Cards.OtherCurrencyOnly)]
    public async Task A_Cash_Schedule_Without_A_Usable_Card_In_The_Address_Currency_Is_Refused(Cards cards)
    {
        _savedCards = Holding(cards);

        AssertRefusedWith(BusinessErrorMessage.OrderCashRequiresSavedCard, await ValidateCreate(PaymentType.Cash));
        AssertRefusedWith(BusinessErrorMessage.OrderCashRequiresSavedCard, await ValidateUpdate(PaymentType.Cash));
    }

    [Fact]
    public async Task A_Card_Expiring_At_The_End_Of_This_Month_Still_Guarantees_A_Cash_Schedule()
    {
        _savedCards = SavedCardDoubles.Holding((userId, currencyId) =>
            currencyId == Czk.Id ? [SavedCardDoubles.CapturedExpiring(userId, currencyId, DateTime.UtcNow)] : []);

        AssertValid(await ValidateCreate(PaymentType.Cash));
        AssertValid(await ValidateUpdate(PaymentType.Cash));
    }

    [Fact]
    public async Task A_Cash_Schedule_Is_Refused_While_The_Customer_Holds_Two_Open_Unpaid_Cash_Bookings()
    {
        ArrangeCustomerOrders(openUnpaidCash: 2);

        AssertRefusedWith(BusinessErrorMessage.OrderCashOpenBookingsLimitReached, await ValidateCreate(PaymentType.Cash));
        AssertRefusedWith(BusinessErrorMessage.OrderCashOpenBookingsLimitReached, await ValidateUpdate(PaymentType.Cash));
    }

    [Fact]
    public async Task One_Open_Unpaid_Cash_Booking_Beside_Orders_That_Are_Not_Leaves_Room_For_A_Cash_Schedule()
    {
        ArrangeCustomerOrders(openUnpaidCash: 1);

        AssertValid(await ValidateCreate(PaymentType.Cash));
        AssertValid(await ValidateUpdate(PaymentType.Cash));
    }

    [Fact]
    public async Task A_Cash_Schedule_Is_Refused_While_The_Customer_Owes_An_Open_Receivable()
    {
        OweAnOpenReceivable();

        AssertRefusedWith(BusinessErrorMessage.OrderCashUnpaidReceivable, await ValidateCreate(PaymentType.Cash));
        AssertRefusedWith(BusinessErrorMessage.OrderCashUnpaidReceivable, await ValidateUpdate(PaymentType.Cash));
    }

    [Fact]
    public async Task A_Card_Schedule_Needs_Neither_A_Saved_Card_Nor_Room_Under_The_Cash_Limit()
    {
        _savedCards = Holding(Cards.None);
        ArrangeCustomerOrders(openUnpaidCash: 2);
        OweAnOpenReceivable();

        AssertValid(await ValidateCreate(PaymentType.Card));
        AssertValid(await ValidateUpdate(PaymentType.Card));
    }

    [Theory]
    [InlineData(Cards.None)]
    [InlineData(Cards.ExpiredLastMonth)]
    [InlineData(Cards.OtherCurrencyOnly)]
    public async Task Confirming_A_Cash_Occurrence_Without_A_Usable_Card_In_Its_Currency_Is_Refused_And_Moves_Nothing(Cards cards)
    {
        _savedCards = Holding(cards);
        var occurrence = ArrangeOccurrence();

        var result = await ConfirmHandler().Handle(new ConfirmRecurringOrder.Command(OccurrenceId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderCashRequiresSavedCard, result.Error!.Message);
        Assert.Equal(nameof(Order.PaymentType), result.Error.Code);
        Assert.Null(occurrence.CustomerConfirmedAt);
        _pending.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Confirming_A_Cash_Occurrence_Is_Refused_While_Two_Other_Open_Unpaid_Cash_Bookings_Are_Held()
    {
        ArrangeCustomerOrders(openUnpaidCash: 2);
        var occurrence = ArrangeOccurrence();

        var result = await ConfirmHandler().Handle(new ConfirmRecurringOrder.Command(OccurrenceId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderCashOpenBookingsLimitReached, result.Error!.Message);
        Assert.Equal(nameof(Order.PaymentType), result.Error.Code);
        Assert.Null(occurrence.CustomerConfirmedAt);
        _pending.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Confirming_A_Cash_Occurrence_Is_Refused_While_The_Customer_Owes_An_Open_Receivable()
    {
        OweAnOpenReceivable();
        var occurrence = ArrangeOccurrence();

        var result = await ConfirmHandler().Handle(new ConfirmRecurringOrder.Command(OccurrenceId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderCashUnpaidReceivable, result.Error!.Message);
        Assert.Equal(nameof(Order.PaymentType), result.Error.Code);
        Assert.Null(occurrence.CustomerConfirmedAt);
        _pending.VerifyNoOtherCalls();
    }

    /// <summary>
    /// The occurrence being confirmed is itself an open unpaid cash order of the customer's, and does not
    /// count against them: it is not a booking until this confirmation.
    /// </summary>
    [Fact]
    public async Task Confirming_A_Cash_Occurrence_Beside_One_Other_Open_Unpaid_Cash_Booking_Is_Accepted()
    {
        ArrangeCustomerOrders(openUnpaidCash: 1);
        var occurrence = ArrangeOccurrence();

        var result = await ConfirmHandler().Handle(new ConfirmRecurringOrder.Command(OccurrenceId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(occurrence.CustomerConfirmedAt);
    }

    private void OweAnOpenReceivable() =>
        _receivables.Setup(r => r.HasOpenForUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

    private static ISavedCardRepository Holding(Cards cards) => SavedCardDoubles.Holding((userId, currencyId) => cards switch
    {
        Cards.ExpiredLastMonth => [SavedCardDoubles.CapturedExpiring(userId, currencyId, DateTime.UtcNow.AddMonths(-1))],
        Cards.OtherCurrencyOnly when currencyId != Czk.Id => [SavedCardDoubles.Usable(userId, currencyId)],
        _ => [],
    });

    /// <summary>
    /// The first <paramref name="openUnpaidCash"/> of the two counted shapes - a one-off cash booking
    /// not yet paid, and a recurring cash occurrence the customer confirmed - beside the five that are not.
    /// </summary>
    private void ArrangeCustomerOrders(int openUnpaidCash)
    {
        _customerOrders.AddRange(new[]
        {
            CustomerOrder(PaymentType.Cash, PaymentStatus.Pending, OrderStatus.New),
            CustomerOrder(PaymentType.Cash, PaymentStatus.Pending, OrderStatus.Confirmed, "tpl-other")
                .ConfirmByCustomer(DateTime.UtcNow),
        }.Take(openUnpaidCash));
        _customerOrders.AddRange(
        [
            CustomerOrder(PaymentType.Cash, PaymentStatus.Pending, OrderStatus.Cancelled),
            CustomerOrder(PaymentType.Cash, PaymentStatus.Paid, OrderStatus.InProgress),
            CustomerOrder(PaymentType.Cash, PaymentStatus.Pending, OrderStatus.Completed),
            CustomerOrder(PaymentType.Cash, PaymentStatus.Pending, OrderStatus.New, "tpl-other"),
            CustomerOrder(PaymentType.Card, PaymentStatus.Pending, OrderStatus.New),
        ]);
    }

    private Order ArrangeOccurrence()
    {
        var order = CustomerOrder(PaymentType.Cash, PaymentStatus.Pending, OrderStatus.New, TemplateId);
        order.Id = OccurrenceId;
        order.TenantId = "company-of-the-order";
        order.SetCurrency(Czk);
        order.UpdateEstimatedTime(120);
        order.CalculateRequiredEmployees(BookingPolicy.SpareSeatsPerOrder);
        _customerOrders.Add(order);
        _orders.Setup(r => r.GetByIdAsync(OccurrenceId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    private static Order CustomerOrder(
        PaymentType paymentType, PaymentStatus paymentStatus, OrderStatus status, string? recurringTemplateId = null)
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana.novakova@example.com",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: paymentType,
            totalPrice: 990m,
            currencyId: Czk.Id,
            paymentStatus: paymentStatus,
            userId: UserId,
            recurringTemplateId: recurringTemplateId);
        order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        return order;
    }

    private Task<ValidationResult> ValidateCreate(PaymentType paymentType) =>
        new CreateRecurringBooking.Validator(
                _orders.Object,
                _session.Object,
                _savedAddresses.Object,
                OrderMarketDoubles.Trading(Czk),
                OrderMarketDoubles.Servicing(CountryId),
                CatalogueDoubles.Services(TwoHours),
                CatalogueDoubles.Packages(),
                Cleansia.Tests.Features.Legal.CustomerConsentDoubles.Consented(),
                Mock.Of<ILegalDocumentResolver>(),
                _savedCards,
                _receivables.Object)
            .ValidateAsync(new CreateRecurringBooking.Command(
                Frequency: (int)RecurrenceFrequency.Weekly,
                DayOfWeek: (int)System.DayOfWeek.Tuesday,
                TimeOfDay: "09:00",
                Rooms: 2,
                Bathrooms: 1,
                SavedAddressId: SavedAddressId,
                SelectedServiceIds: [TwoHours.Id],
                SelectedPackageIds: [],
                PaymentType: (int)paymentType,
                StartsOn: DateTime.UtcNow.AddDays(3),
                EarlyPerformanceRequested: true));

    private Task<ValidationResult> ValidateUpdate(PaymentType paymentType) =>
        new UpdateRecurringBooking.Validator(
                _templates.Object,
                _memberships.Object,
                _session.Object,
                _orders.Object,
                _savedAddresses.Object,
                OrderMarketDoubles.Trading(Czk),
                OrderMarketDoubles.Servicing(CountryId),
                CatalogueDoubles.Services(TwoHours),
                CatalogueDoubles.Packages(),
                _savedCards,
                _receivables.Object)
            .ValidateAsync(new UpdateRecurringBooking.Command(
                TemplateId: TemplateId,
                Frequency: (int)RecurrenceFrequency.Weekly,
                DayOfWeek: (int)System.DayOfWeek.Tuesday,
                TimeOfDay: "11:30",
                Rooms: 2,
                Bathrooms: 1,
                SavedAddressId: SavedAddressId,
                SelectedServiceIds: [TwoHours.Id],
                SelectedPackageIds: [],
                PaymentType: (int)paymentType,
                StartsOn: DateTime.UtcNow.AddDays(3)));

    private ConfirmRecurringOrder.Handler ConfirmHandler() => new(
        OrderAccessDoubles.Over(_orders, _session),
        _orders.Object,
        _savedCards,
        _receivables.Object,
        Mock.Of<ICreditAccountRepository>(),
        Mock.Of<IUserRepository>(),
        _session.Object,
        Mock.Of<ITenantProvider>(),
        Mock.Of<IStripeClient>(),
        new StripeConfig(new ConfigurationBuilder().Build()),
        Mock.Of<IStripeCustomerResolver>(),
        Mock.Of<IRequestMetadataProvider>(),
        new OrderChannelProvider(OrderChannel.Mobile),
        _pending.Object,
        Mock.Of<INotificationProducer>(),
        NoPreferredCleanerHold.Resolver,
        Mock.Of<IAdminNotifier>(),
        new AuditContext(),
        NullLogger<ConfirmRecurringOrder.Handler>.Instance);

    private static void AssertRefusedWith(string key, ValidationResult result)
    {
        var error = Assert.Single(result.Errors);
        Assert.Equal(key, error.ErrorMessage);
        Assert.Equal(nameof(CreateRecurringBooking.Command.PaymentType), error.PropertyName);
    }

    private static void AssertValid(ValidationResult result) =>
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));

    private static SavedAddress SavedAddressInCzechia()
    {
        var address = Address.Create("Testovaci 12", "Praha", "11000", CountryId);
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        typeof(SavedAddress).GetProperty(nameof(SavedAddress.Address))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(saved, [address]);
        return saved;
    }

    private static RecurringBookingTemplate Template()
    {
        var template = RecurringBookingTemplate.Create(
            userId: UserId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: System.DayOfWeek.Tuesday,
            timeOfDay: new TimeOnly(9, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: SavedAddressId,
            selectedServiceIds: [TwoHours.Id],
            selectedPackageIds: [],
            paymentType: PaymentType.Cash,
            startsOn: DateTime.UtcNow.AddDays(1));
        template.Id = TemplateId;
        return template;
    }

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
            currencyId: Czk.Id,
            stripeSubscriptionId: "sub_cash_standing",
            currentPeriodStart: DateTime.UtcNow.AddDays(-1),
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
    }
}
