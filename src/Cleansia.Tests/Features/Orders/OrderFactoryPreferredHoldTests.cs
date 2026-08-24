using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.TestUtilities.MockDataFactories.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0036 D2 — the factory hands the resolver's answer to the aggregate and writes neither hold
/// column itself. The preference and the deadline are two columns with two lifetimes: a declined hold
/// still stores what the customer asked for, because "we stored your preference but could not act on
/// it" has to be expressible.
/// </summary>
public class OrderFactoryPreferredHoldTests
{
    private const string PreferredEmployeeId = "employee-favourite";

    private static readonly DateTime Now = new(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<IPackageRepository> _packageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IVatCalculator> _vatCalculator = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepository = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly List<ProducedPush> _pushes = [];

    public OrderFactoryPreferredHoldTests()
    {
        _notificationProducer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (userId, eventKey, args, tenantId, subject, _) =>
                    _pushes.Add(new ProducedPush(userId, eventKey, args, tenantId, subject)))
            .Returns(Task.CompletedTask);

        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        _packageRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());
    }

    [Fact]
    public async Task A_Granted_Hold_Is_Written_As_The_Pair()
    {
        var holdUntilUtc = Now.AddHours(2);

        var order = await CreateAsync(NoPreferredCleanerHold.Grants(holdUntilUtc));

        Assert.Equal(PreferredEmployeeId, order.PreferredEmployeeId);
        Assert.Equal(holdUntilUtc, order.PreferredHoldUntilUtc);
    }

    [Fact]
    public async Task A_Declined_Hold_Still_Stores_The_Preference()
    {
        var order = await CreateAsync(NoPreferredCleanerHold.Resolver);

        Assert.Equal(PreferredEmployeeId, order.PreferredEmployeeId);
        Assert.Null(order.PreferredHoldUntilUtc);
    }

    /// <summary>
    /// The window the resolver is asked about is the booking's own — its country, its instant, its
    /// derived length, and the caller's clock rather than a second reading of it.
    /// </summary>
    [Fact]
    public async Task The_Resolver_Is_Asked_About_The_Booking_Being_Created()
    {
        var resolver = new Mock<IPreferredCleanerHoldResolver>();
        resolver
            .Setup(r => r.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PreferredCleanerOutcome.Declined(HoldDeclineReason.NoMembership));

        var input = Input();
        await NewFactory(resolver.Object).CreateAsync(input, CancellationToken.None);

        resolver.Verify(
            r => r.ResolveAsync(
                input.UserId,
                PreferredEmployeeId,
                input.Address.CountryId,
                input.CleaningDate,
                0,
                input.NowUtc,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A hold with no signal is pure board latency, so the two ship together or not at all — ADR-0036
    /// D10's "if only one can ship, ship neither", made checkable. The addressee rides on the resolver's
    /// answer, so a granted hold cannot be produced without one.
    /// </summary>
    [Fact]
    public async Task A_Granted_Hold_Sends_The_Targeted_Offer_To_The_Cleaner()
    {
        var order = await CreateAsync(NoPreferredCleanerHold.Grants(Now.AddHours(2)));

        var push = Assert.Single(_pushes);
        Assert.Equal(NoPreferredCleanerHold.RecipientUserId, push.UserId);
        Assert.Equal(NotificationEventCatalog.PreferredOffer, push.EventKey);
        Assert.Equal(order.Id, push.Args["orderId"]);
        Assert.Equal(order.DisplayOrderNumber, push.Args["orderNumber"]);
        // The subject carries the reservation ROUND now, not just the order — an order may hold more
        // than one preferred reservation and the two closures would otherwise share a key.
        Assert.Equal($"{order.Id}:{order.PreferredOfferRound}", push.Subject);
    }

    /// <summary>
    /// The one-way invariant, from the other side: a decline means the cleaner cannot act on the offer,
    /// so telling them about it is noise on the channel the mechanism depends on being worth reading.
    /// </summary>
    [Fact]
    public async Task A_Declined_Outcome_Sends_Nothing()
    {
        await CreateAsync(NoPreferredCleanerHold.Resolver);

        Assert.Empty(_pushes);
    }

    /// <summary>
    /// Q-BROWSE-01 (b). A card order is <c>New</c> + <c>Pending</c> when it leaves the factory, so it is
    /// not offerable: the browse gate refuses it and <c>CleanupStalePendingOrders</c> may cancel it
    /// within 15 minutes. The hold is still granted — the seat is genuinely withheld — but the
    /// announcement waits for the Stripe webhook, which is the write that makes the job real.
    /// </summary>
    [Fact]
    public async Task A_Card_Booking_Grants_The_Hold_And_Announces_Nothing_Yet()
    {
        var holdUntilUtc = Now.AddHours(2);

        var order = await CreateAsync(
            NoPreferredCleanerHold.Grants(holdUntilUtc), Input(PaymentType.Card));

        Assert.Equal(holdUntilUtc, order.PreferredHoldUntilUtc);
        Assert.Empty(_pushes);
    }

    /// <summary>
    /// The same, for the case the money axis refuses regardless of payment type: anything carrying a
    /// <c>RecurringTemplateId</c> survives only via <c>Paid</c>, so a materialized occurrence is
    /// announced by the customer's own confirmation, not by the materializer.
    /// </summary>
    [Fact]
    public async Task A_Recurring_Cash_Occurrence_Grants_The_Hold_And_Announces_Nothing_Yet()
    {
        var holdUntilUtc = Now.AddHours(2);

        var order = await CreateAsync(
            NoPreferredCleanerHold.Grants(holdUntilUtc),
            Input(PaymentType.Cash) with { RecurringTemplateId = "tmpl-weekly-factory" });

        Assert.Equal(holdUntilUtc, order.PreferredHoldUntilUtc);
        Assert.Empty(_pushes);
    }

    /// <summary>
    /// The 2-to-8-hour band: too little lead time to withhold a seat, enough to be told. This is what
    /// makes the 8-hour floor a reduction of the perk rather than its absence.
    /// </summary>
    [Fact]
    public async Task A_Short_Lead_Booking_Sends_The_Offer_With_No_Hold()
    {
        var resolver = new Mock<IPreferredCleanerHoldResolver>();
        resolver
            .Setup(r => r.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PreferredCleanerOutcome.NotifyOnly(
                HoldDeclineReason.ShortLeadTime,
                new PreferredCleanerRecipient(NoPreferredCleanerHold.RecipientUserId, null)));

        var order = await CreateAsync(resolver.Object);

        Assert.Null(order.PreferredHoldUntilUtc);
        Assert.Equal(NotificationEventCatalog.PreferredOffer, Assert.Single(_pushes).EventKey);
    }

    private sealed record ProducedPush(
        string UserId,
        string EventKey,
        Dictionary<string, string> Args,
        string? TenantId,
        string? Subject);

    private async Task<Cleansia.Core.Domain.Orders.Order> CreateAsync(
        IPreferredCleanerHoldResolver resolver, CreateOrderInput? input = null) =>
        await NewFactory(resolver).CreateAsync(input ?? Input(), CancellationToken.None);

    private OrderFactory NewFactory(IPreferredCleanerHoldResolver resolver) =>
        new(
            _orderRepository.Object,
            _serviceRepository.Object,
            _packageRepository.Object,
            PayConfigRepositoryDouble.Holding(),
            _companyInfoRepository.Object,
            _countryConfigurationRepository.Object,
            _vatCalculator.Object,
            _loyaltyService.Object,
            _userMembershipRepository.Object,
            resolver,
            _notificationProducer.Object);

    /// <summary>
    /// A one-off CASH booking by default — the one shape that is offerable the instant it exists, which
    /// is why the announcement rides creation for it and for nothing else.
    /// </summary>
    private static CreateOrderInput Input(PaymentType paymentType = PaymentType.Cash) =>
        new(
            UserId: null,
            CustomerName: "Test Customer",
            CustomerEmail: "customer@example.com",
            CustomerPhone: "+420123456789",
            Address: AddressMockFactory.Generate(),
            Rooms: 2,
            Bathrooms: 1,
            Extras: new Dictionary<string, bool>(),
            CleaningDate: Now.AddDays(3),
            PaymentType: paymentType,
            Currency: Currency.Create("CZK", "Kč", "Czech Koruna", 1m),
            SelectedServiceIds: ["service-1"],
            SelectedPackageIds: [],
            RawSubtotal: 1500m,
            NowUtc: Now,
            ReservedExpressWaiver: null,
            PreferredEmployeeId: PreferredEmployeeId);
}
