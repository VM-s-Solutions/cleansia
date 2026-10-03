using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.TestUtilities.MockDataFactories.Users;
using MockQueryable;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ToS §19: a booking stays under the terms version it was made under. The factory is the one
/// construction path of one-off and recurring orders, so it is where the cancellation schedule in force
/// at booking is frozen on the order — the cancel and both previews read it from there.
/// </summary>
public class OrderFactoryCancellationTermsTests
{
    private const string ServiceId = "service-cancellation-terms";
    private static readonly Currency Czk = CreateOrderTestData.DefaultCurrency();

    private readonly Mock<IServiceRepository> _serviceRepository = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();

    public OrderFactoryCancellationTermsTests()
    {
        _loyaltyService
            .Setup(s => s.ResolveTierDiscountForOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierDiscountResult(0m, null));

        var service = Service.Create("category-cancellation-terms", "Service", "Under test", 120);
        service.Id = ServiceId;
        _serviceRepository
            .Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(new[] { service }.AsQueryable().BuildMock());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("template-weekly")]
    public async Task A_One_Off_Booking_And_A_Recurring_Visit_Are_Frozen_Under_Todays_Schedule(
        string? recurringTemplateId)
    {
        var order = await CreateFactory().CreateAsync(Input(recurringTemplateId), CancellationToken.None);

        Assert.Equal(BookingPolicy.FreeCancellationHours, order.CancellationFreeHours);
        Assert.Equal(BookingPolicy.PartialCancellationHours, order.CancellationPartialHours);
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, order.CancellationPartialFeeRate);
        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, order.CancellationLastMinuteFeeRate);
        Assert.Equal(BookingPolicy.PlusFreeCancellationHours, order.CancellationPlusFreeHours);
    }

    private OrderFactory CreateFactory()
    {
        var packages = new Mock<IPackageRepository>();
        packages.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());

        return new OrderFactory(
            Mock.Of<IOrderRepository>(),
            _serviceRepository.Object,
            packages.Object,
            ExtraRepositoryDouble.Empty(),
            CataloguePriceDoubles.Services(Czk, (ServiceId, 500m, 0m)),
            CataloguePriceDoubles.Packages(Czk),
            CataloguePriceDoubles.NoExtras(),
            PayConfigRepositoryDouble.Covering(CreateOrderTestData.CurrencyId, [ServiceId], []),
            Mock.Of<ICompanyInfoRepository>(),
            Mock.Of<ICountryConfigurationRepository>(),
            Mock.Of<IVatCalculator>(),
            _loyaltyService.Object,
            Mock.Of<IUserMembershipRepository>(),
            NoPreferredCleanerHold.Resolver,
            WorkContractResolvers.Resolver().Object,
            Mock.Of<INotificationProducer>(),
            Mock.Of<IAdminNotifier>(),
            NullLogger<OrderFactory>.Instance);
    }

    private static CreateOrderInput Input(string? recurringTemplateId) =>
        new(
            UserId: "user-cancellation-terms",
            CustomerName: "Test Customer",
            CustomerEmail: "customer@example.com",
            CustomerPhone: "+420123456789",
            Address: AddressMockFactory.Generate(),
            Rooms: 2,
            Bathrooms: 1,
            SelectedExtraSlugs: [],
            CleaningDate: DateTime.UtcNow.AddDays(3),
            PaymentType: PaymentType.Card,
            Currency: Czk,
            SelectedServiceIds: [ServiceId],
            SelectedPackageIds: [],
            RawSubtotal: 500m,
            NowUtc: DateTime.UtcNow,
            ReservedExpressWaiver: null,
            OperatorTenantId: null,
            RecurringTemplateId: recurringTemplateId);
}
