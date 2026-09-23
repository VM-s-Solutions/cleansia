using System.Security.Claims;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Refunds;

[Collection("PostgresCollection")]
public class PartialRefundExtrasPersistenceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "order-partial-extras";
    private const string ServiceId = "service-partial-extras";
    private const string CountryId = "country-partial-extras";

    [Fact]
    public async Task Refunding_A_Persisted_Service_Leaves_The_Extra_Out_Of_The_Refund()
    {
        RefundRequest? issued = null;
        var refunds = new Mock<IRefundService>();
        refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((request, _) => issued = request)
            .ReturnsAsync((RefundRequest request, CancellationToken _) => BusinessResult.Success(new RefundResult(
                "refund-partial-extras", "refund:partial-extras", request.Amount, RefundStatus.Succeeded, false)));

        await TestMethod(
            setup: services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
                    "admin-partial-extras", "admin-extras@cleansia.test",
                    [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
                services.Replace(ServiceDescriptor.Scoped<IRefundService>(_ => refunds.Object));
                services.Replace(ServiceDescriptor.Scoped<ILoyaltyService>(_ => Mock.Of<ILoyaltyService>()));
                return Task.CompletedTask;
            },
            arrange: SeedCompletedOrderAsync,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new IssuePartialRefund.Command(
                OrderId, [new IssuePartialRefund.RefundLineSelection(ServiceId, null)],
                RefundReason.ServiceNotRendered, OverrideReason: null)),
            assert: async (CleansiaDbContext context, BusinessResult<IssuePartialRefund.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.NotNull(issued);
                Assert.Equal(1000m, issued.Amount);
                Assert.Equal(1000m, result.Value!.RefundAmount);
                var extra = Assert.Single(await context.Set<OrderExtra>().Where(e => e.OrderId == OrderId).ToListAsync());
                Assert.Equal(200m, extra.UnitPrice);
                Assert.Equal(1200m, (await context.Orders.SingleAsync(o => o.Id == OrderId)).TotalPrice);
            });
    }

    private static async Task SeedCompletedOrderAsync(CleansiaDbContext context)
    {
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        context.Currencies.Add(currency);
        var category = ServiceCategory.Create("partial-extras", "Cleaning", "Refund fixture");
        context.ServiceCategories.Add(category);
        var service = Service.Create(category.Id, "Cleaning", "Refund fixture", 60);
        service.Id = ServiceId;
        context.Services.Add(service);
        var extra = Extra.Create("partial-extra", "Windows", null);
        context.Extras.Add(extra);

        var order = Order.Create("Customer", "customer-extras@cleansia.test", "+420777123456",
            Address.Create("Testovaci 12", "Praha", "11000", CountryId), 2, 1,
            DateTime.UtcNow.AddDays(-1), PaymentType.Card, 1200m, currency.Id, PaymentStatus.Paid);
        order.Id = OrderId;
        order.AddSelectedServices([OrderService.Create(order, service, 1000m, 0m, 1000m)]);
        order.AddSelectedExtras([OrderExtra.Create(order, extra, 200m)]);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        order.CompleteOrder(60);
        context.Orders.Add(order);
        await context.CommitAsync(CancellationToken.None);
    }
}
