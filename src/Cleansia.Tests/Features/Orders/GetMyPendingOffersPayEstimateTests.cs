using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A job held for a cleaner quotes what that cleaner earns on it, the figure the board, the dashboard and
/// the job detail already quote, and keeps the customer's total for the builds installed before the field.
/// </summary>
public class GetMyPendingOffersPayEstimateTests
{
    private const string EmployeeId = "emp-pending-pay";
    private const string ServiceId = "service-pending-pay";
    private const decimal PayPerJob = 13m;
    private const decimal CustomerTotal = 1000m;

    [Fact]
    public async Task A_Held_Job_Quotes_The_Cleaners_Pay_For_One_Seat()
    {
        var offer = Assert.Single(await OffersFor(HeldOrder("held-one-seat"), PayConfigs()));

        Assert.Equal(PayPerJob, offer.EstimatedCleanerPay);
        Assert.Equal(CustomerTotal, offer.TotalPrice);
    }

    /// <summary>
    /// 13 and the company's 40 % of the 10 of extras booked make 17 for the job, and each of its two
    /// seats is quoted 8.50.
    /// </summary>
    [Fact]
    public async Task A_Two_Seat_Job_Quotes_One_Seats_Share_With_The_Extras_Share()
    {
        var order = HeldOrder("held-two-seats");
        order.UpdateEstimatedTime(240).CalculateRequiredEmployees(spareSeats: 0);
        order.AddSelectedExtras([OrderExtra.Create(order, Extra.Create("windows", "Windows", null), 10m)]);
        var configuration = new Mock<IAppConfigurationProvider>();
        configuration
            .Setup(c => c.GetTenantSettingAsync(TenantSettingCatalog.ExtrasSharePercentKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("40");

        var offer = Assert.Single(await OffersFor(order, PayConfigs(), configuration.Object));

        Assert.Equal(8.50m, offer.EstimatedCleanerPay);
        Assert.Equal(CustomerTotal, offer.TotalPrice);
    }

    [Fact]
    public async Task A_Job_No_Rate_Covers_Quotes_No_Pay()
    {
        var offer = Assert.Single(await OffersFor(HeldOrder("held-unpriced"), payConfigs: []));

        Assert.Null(offer.EstimatedCleanerPay);
        Assert.Equal(CustomerTotal, offer.TotalPrice);
    }

    private static async Task<IReadOnlyList<PendingOfferItem>> OffersFor(
        Order held,
        IReadOnlyList<EmployeePayConfig> payConfigs,
        IAppConfigurationProvider? configuration = null)
    {
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { held }.AsQueryable().BuildMock());

        var payConfigRepository = new Mock<IEmployeePayConfigRepository>();
        payConfigRepository
            .Setup(r => r.GetServiceConfigsForOrderAsync(
                It.IsAny<IEnumerable<string>>(), EmployeeId, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payConfigs);
        payConfigRepository
            .Setup(r => r.GetPackageConfigsForOrderAsync(
                It.IsAny<IEnumerable<string>>(), EmployeeId, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var accessService = new Mock<IOrderAccessService>();
        accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);

        var result = await new GetMyPendingOffers.Handler(
                orderRepository.Object,
                accessService.Object,
                ValidatorTestHelpers.CurrencyResolver(),
                payConfigRepository.Object,
                configuration ?? Mock.Of<IAppConfigurationProvider>())
            .Handle(new GetMyPendingOffers.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static IReadOnlyList<EmployeePayConfig> PayConfigs() =>
        [EmployeePayConfig.CreateForService(ServiceId, PayPerJob, ValidatorTestHelpers.CurrencyId)];

    private static Order HeldOrder(string orderId)
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(
            orderId, OrderStatus.New, maxEmployees: 1, paymentType: PaymentType.Card, paymentStatus: PaymentStatus.Paid);
        var currency = Core.Domain.Internationalization.Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = ValidatorTestHelpers.CurrencyId;
        order.SetCurrency(currency);

        var service = Service.Create("category-pending-pay", "Deep clean", "Payable", 120);
        service.Id = ServiceId;
        order.AddSelectedServices([OrderLineMockFactory.ServiceLine(order, service)]);

        order.GrantPreferredHold(
            EmployeeId, DateTime.UtcNow.AddHours(2), DateTime.UtcNow, BookingPolicy.MaxPreferredOfferRounds);
        return order;
    }
}
