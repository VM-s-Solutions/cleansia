using Cleansia.Core.AppServices.Auditing;
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
using Cleansia.Tests.Features.Legal;
using Cleansia.Tests.Features.Orders;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// Owner ruling 2026-09-28 (decision 34): a schedule carries the dirtiness level the customer picked,
/// Normal when a client sends none, and every occurrence it spawns is priced and stored at that level.
/// </summary>
public class RecurringTemplateDirtinessLevelTests
{
    private const string TemplateId = "template-level-1";
    private const string UserId = "user-level-1";
    private const string SavedAddressId = "saved-address-level-1";

    private readonly Mock<IRecurringBookingTemplateRepository> _templateRepository = new();
    private readonly Mock<ISavedAddressRepository> _savedAddressRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public RecurringTemplateDirtinessLevelTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        var address = AddressMockFactory.Generate();
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        SetPrivate(saved, nameof(SavedAddress.Address), address);
        _savedAddressRepository
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([saved]);
        _savedAddressRepository
            .Setup(r => r.GetByIdAsync(SavedAddressId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);
    }

    [Theory]
    [InlineData(DirtinessLevel.Increased)]
    [InlineData(DirtinessLevel.Heavy)]
    public async Task A_Schedule_Is_Created_At_The_Level_The_Customer_Picked(DirtinessLevel level)
    {
        RecurringBookingTemplate? added = null;
        _templateRepository
            .Setup(r => r.Add(It.IsAny<RecurringBookingTemplate>()))
            .Callback((RecurringBookingTemplate t) => added = t);

        var result = await CreateHandler().Handle(CreateCommand() with { DirtinessLevel = level }, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(level, added!.DirtinessLevel);
        Assert.Equal(level, result.Value!.DirtinessLevel);
    }

    [Fact]
    public async Task A_Client_That_Sends_No_Level_Creates_A_Normal_Schedule()
    {
        RecurringBookingTemplate? added = null;
        _templateRepository
            .Setup(r => r.Add(It.IsAny<RecurringBookingTemplate>()))
            .Callback((RecurringBookingTemplate t) => added = t);

        var result = await CreateHandler().Handle(CreateCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(DirtinessLevel.Normal, added!.DirtinessLevel);
    }

    [Fact]
    public async Task An_Edit_Replaces_The_Level()
    {
        var existing = Template(DirtinessLevel.Heavy);
        _templateRepository
            .Setup(r => r.GetByIdForOwnerAsync(TemplateId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await new UpdateRecurringBooking.Handler(
                _templateRepository.Object, _savedAddressRepository.Object, _session.Object,
                OrderMarketDoubles.OperatedBy("cleansia-cz"), Mock.Of<ICountryConfigurationRepository>(), new AuditContext())
            .Handle(new UpdateRecurringBooking.Command(
                TemplateId: TemplateId,
                Frequency: (int)RecurrenceFrequency.Monthly,
                DayOfWeek: (int)System.DayOfWeek.Tuesday,
                TimeOfDay: "09:00",
                Rooms: 2,
                Bathrooms: 1,
                SavedAddressId: SavedAddressId,
                SelectedServiceIds: [CreateOrderTestData.ServiceId],
                SelectedPackageIds: [],
                PaymentType: (int)PaymentType.Card,
                StartsOn: DateTime.UtcNow.AddDays(3),
                DirtinessLevel: DirtinessLevel.Increased), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(DirtinessLevel.Increased, existing.DirtinessLevel);
        Assert.Equal(DirtinessLevel.Increased, result.Value!.DirtinessLevel);
    }

    [Fact]
    public async Task The_List_Returns_Each_Schedules_Level()
    {
        var heavy = Template(DirtinessLevel.Heavy);
        _templateRepository
            .Setup(r => r.GetByUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([heavy]);

        var result = await new GetMyRecurringBookings.Handler(
                _templateRepository.Object, _savedAddressRepository.Object, _session.Object,
                Mock.Of<IServiceRepository>(), Mock.Of<IPackageRepository>(), Mock.Of<ICountryConfigurationRepository>())
            .Handle(new GetMyRecurringBookings.Query(), CancellationToken.None);

        Assert.Equal(DirtinessLevel.Heavy, Assert.Single(result.Value!).DirtinessLevel);
    }

    [Theory]
    [InlineData(DirtinessLevel.Normal)]
    [InlineData(DirtinessLevel.Heavy)]
    public async Task Every_Occurrence_Is_Priced_And_Stored_At_The_Schedules_Level(DirtinessLevel level)
    {
        var template = Template(level, dayOfWeek: DateTime.UtcNow.AddDays(2).DayOfWeek, startsOn: DateTime.UtcNow.AddDays(-7));
        var user = User.CreateWithPassword("level@cleansia.test", "Password1!", "Lea", "Level", UserProfile.Customer);
        user.Id = UserId;
        SetPrivate(template, nameof(RecurringBookingTemplate.User), user);
        _templateRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { template }.AsQueryable().BuildMock());

        var orders = new Mock<IOrderRepository>();
        orders
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(Array.Empty<Cleansia.Core.Domain.Orders.Order>().AsQueryable().BuildMock());
        var memberships = new Mock<IUserMembershipRepository>();
        memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembershipMockFactory.Paid(UserId));
        var pricing = new Mock<IOrderPricingCalculator>();
        pricing
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<DirtinessLevel>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
        var inputs = new List<CreateOrderInput>();
        var factory = new Mock<IOrderFactory>();
        factory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .Callback((CreateOrderInput input, CancellationToken _) => inputs.Add(input))
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial { Id = "order-level-1", UserId = UserId }));

        var result = await new MaterializeRecurringBookingTemplate.Handler(
                _templateRepository.Object,
                _savedAddressRepository.Object,
                Mock.Of<IAddressRepository>(),
                Mock.Of<ICountryConfigurationRepository>(),
                OrderMarketDoubles.Trading(Currency.Create("CZK", "Kč", "Czech Koruna")),
                orders.Object,
                pricing.Object,
                factory.Object,
                memberships.Object,
                OrderMarketDoubles.OperatedBy("cleansia-cz"),
                Mock.Of<ITenantProvider>(),
                Mock.Of<IUnitOfWork>(),
                NullLogger<MaterializeRecurringBookingTemplate.Handler>.Instance,
                Mock.Of<INotificationProducer>())
            .Handle(new MaterializeRecurringBookingTemplate.Command(TemplateId, DateTime.UtcNow, HorizonDays: 7), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        pricing.Verify(c => c.CalculateAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
            level,
            It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotEmpty(inputs);
        Assert.All(inputs, input => Assert.Equal(level, input.DirtinessLevel));
    }

    private CreateRecurringBooking.Handler CreateHandler()
    {
        var memberships = new Mock<IUserMembershipRepository>();
        memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembership.Create(
                userId: UserId,
                membershipPlanId: "plan-plus",
                currencyId: "currency-czk",
                stripeSubscriptionId: "sub_1",
                currentPeriodStart: DateTime.UtcNow.AddDays(-1),
                currentPeriodEnd: DateTime.UtcNow.AddMonths(1)));

        return new CreateRecurringBooking.Handler(
            _templateRepository.Object,
            _savedAddressRepository.Object,
            memberships.Object,
            _session.Object,
            OrderMarketDoubles.OperatedBy("cleansia-cz"),
            Mock.Of<ICountryConfigurationRepository>(),
            Mock.Of<IConsentService>(),
            CustomerConsentDoubles.Consented(),
            Mock.Of<ILegalDocumentResolver>(),
            new AuditContext(),
            new Cleansia.Core.AppServices.Authentication.HostAudienceProvider("cleansia.customer"),
            new Cleansia.TestUtilities.TestRequestMetadataProvider());
    }

    private static CreateRecurringBooking.Command CreateCommand() =>
        new(
            Frequency: (int)RecurrenceFrequency.Monthly,
            DayOfWeek: (int)System.DayOfWeek.Tuesday,
            TimeOfDay: "09:00",
            Rooms: 2,
            Bathrooms: 1,
            SavedAddressId: SavedAddressId,
            SelectedServiceIds: [CreateOrderTestData.ServiceId],
            SelectedPackageIds: [],
            PaymentType: (int)PaymentType.Card,
            StartsOn: DateTime.UtcNow.AddDays(3));

    private static RecurringBookingTemplate Template(
        DirtinessLevel level, System.DayOfWeek dayOfWeek = System.DayOfWeek.Tuesday, DateTime? startsOn = null)
    {
        var template = RecurringBookingTemplate.Create(
            userId: UserId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: dayOfWeek,
            timeOfDay: new TimeOnly(10, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: SavedAddressId,
            selectedServiceIds: [CreateOrderTestData.ServiceId],
            selectedPackageIds: [],
            paymentType: PaymentType.Card,
            startsOn: startsOn ?? DateTime.UtcNow.AddDays(3),
            dirtinessLevel: level);
        template.Id = TemplateId;
        return template;
    }

    private static void SetPrivate<T>(T target, string property, object value) =>
        typeof(T).GetProperty(property)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(target, [value]);
}
