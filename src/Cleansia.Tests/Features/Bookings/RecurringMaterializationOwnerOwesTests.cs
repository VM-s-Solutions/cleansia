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
/// Owner ruling 2026-10-06: a customer who owes any company money makes no new booking, and their schedules
/// create no visits either, so they are never asked to confirm one. The schedule itself is kept and resumes
/// once the debt is paid or written off. The two legs differ only in the debt.
/// </summary>
public sealed class RecurringMaterializationOwnerOwesTests
{
    private const string TemplateId = "template-owes";
    private const string UserId = "user-owes";
    private const string SavedAddressId = "saved-address-owes";

    private readonly Mock<IOrderFactory> _orderFactory = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly RecurringBookingTemplate _template = Template();

    [Fact]
    public async Task An_Owner_Who_Owes_Nothing_Gets_Their_Visits()
    {
        var result = await Handle();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.OrdersCreated > 0);
    }

    [Fact]
    public async Task An_Owner_Who_Owes_An_Open_Receivable_Gets_No_Visit_And_Keeps_The_Schedule()
    {
        _receivables.Setup(r => r.HasOpenForUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Handle();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.OrdersCreated);
        _orderFactory.Verify(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(_template.IsActive);
        Assert.Null(_template.LastMaterializedFor);
    }

    private Task<Cleansia.Infra.Common.Validations.BusinessResult<MaterializeRecurringBookingTemplate.Response>> Handle()
    {
        var templates = new Mock<IRecurringBookingTemplateRepository>();
        templates.Setup(r => r.GetQueryableIgnoringTenant()).Returns(new[] { _template }.AsQueryable().BuildMock());

        var address = AddressMockFactory.Generate();
        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        var savedAddresses = new Mock<ISavedAddressRepository>();
        savedAddresses.Setup(r => r.GetByIdAsync(SavedAddressId, It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        var addresses = new Mock<IAddressRepository>();
        addresses.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(address);

        var orders = new Mock<IOrderRepository>();
        orders.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(Array.Empty<Cleansia.Core.Domain.Orders.Order>().AsQueryable().BuildMock());

        var pricing = new Mock<IOrderPricingCalculator>();
        pricing
            .Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<DirtinessLevel>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
        _orderFactory
            .Setup(f => f.CreateAsync(It.IsAny<CreateOrderInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
            {
                Id = "order-owes",
                UserId = UserId,
                PaymentType = PaymentType.Card,
            }));

        var memberships = new Mock<IUserMembershipRepository>();
        memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserMembership.Create(
                UserId, "plan-owes", "currency-czk", "sub_owes", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(20), null));

        return new MaterializeRecurringBookingTemplate.Handler(
                templates.Object,
                savedAddresses.Object,
                addresses.Object,
                Mock.Of<ICountryConfigurationRepository>(),
                OrderMarketDoubles.Trading(Currency.Create("CZK", "Kč", "Czech Koruna")),
                orders.Object,
                pricing.Object,
                _orderFactory.Object,
                memberships.Object,
                _receivables.Object,
                OrderMarketDoubles.OperatedBy("cleansia-cz"),
                Mock.Of<ITenantProvider>(),
                _unitOfWork.Object,
                NullLogger<MaterializeRecurringBookingTemplate.Handler>.Instance,
                Mock.Of<INotificationProducer>())
            .Handle(new MaterializeRecurringBookingTemplate.Command(TemplateId, DateTime.UtcNow, HorizonDays: 7), CancellationToken.None);
    }

    private static RecurringBookingTemplate Template()
    {
        var user = User.CreateWithPassword("owes@cleansia.test", "Password1!", "Owes", "Money", UserProfile.Customer);
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
            paymentType: PaymentType.Card,
            startsOn: DateTime.UtcNow.AddDays(-7));
        template.Id = TemplateId;
        typeof(RecurringBookingTemplate).GetProperty(nameof(RecurringBookingTemplate.User))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(template, [user]);
        return template;
    }
}
