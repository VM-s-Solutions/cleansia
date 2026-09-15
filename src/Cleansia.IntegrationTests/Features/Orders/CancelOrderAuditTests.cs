using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// ADR-0062 D3 through the real pipeline on real Postgres: a customer cancelling a paid card order three
/// hours before an accepted job leaves ONE <c>customer.order.cancel</c> success row, committed with the
/// cancellation, whose payload carries the fee tier and the figures it was charged by; a cancel refused
/// on an in-progress order leaves one out-of-band failure row with the refusal KEY and the order id and
/// no payload; a probe at another customer's order leaves the prober's failure row pointing at the
/// victim's order, and the victim's order untouched.
/// </summary>
[Collection("PostgresCollection")]
public class CancelOrderAuditTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CustomerId = "user-cancel-audit-1";
    private const string OtherCustomerId = "user-cancel-audit-2";
    private const string OrderId = "order-cancel-audit-1";
    private const string CurrencyId = "currency-czk-cancel-audit";
    private const string CountryId = "country-cz-cancel-audit";
    private const string Ip = "203.0.113.9";
    private const string DeviceLabel = "iPhone 15 / iOS 17.4";

    private static Task CustomerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CustomerId, "cancel-audit@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
        services.Replace(ServiceDescriptor.Scoped<IRequestMetadataProvider>(_ => new TestRequestMetadataProvider(Ip, DeviceLabel, "device-1")));
        services.Replace(ServiceDescriptor.Scoped<IRefundService>(_ => new SucceedingRefunds()));
        return Task.CompletedTask;
    }

    private static Func<CleansiaDbContext, Task> Seed(
        string ownerUserId = CustomerId,
        OrderStatus lastStatus = OrderStatus.Confirmed,
        bool accepted = true) => async context =>
    {
        context.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var customer = User.CreateWithPassword("cancel-audit@cleansia.test", "Seed-Password-123", "Cancel", "Audit");
        customer.Id = CustomerId;
        var other = User.CreateWithPassword("cancel-audit-2@cleansia.test", "Seed-Password-123", "Other", "Customer");
        other.Id = OtherCustomerId;
        var cleanerUser = User.CreateWithPassword("cleaner-cancel-audit@cleansia.test", "Seed-Password-123", "Clean", "Er", UserProfile.Employee);
        var cleaner = Employee.CreateWithUser(cleanerUser);
        context.Users.AddRange(customer, other, cleanerUser);
        context.Employees.Add(cleaner);

        var order = Order.Create(
            customerName: "Cancel Audit",
            customerEmail: "cancel-audit@cleansia.test",
            customerPhone: "+420777111222",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: ownerUserId);
        order.Id = OrderId;
        // Booked two days ago, so the oops window cannot mask the tier.
        order.Created("seed", DateTimeOffset.UtcNow.AddDays(-2));
        order.AssignStripeSessionId("cs_test_cancel_audit");
        order.AssignStripePaymentIntentId("pi_test_cancel_audit");
        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var status in new[] { OrderStatus.New, lastStatus })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("seed", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        if (accepted)
        {
            order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        }

        context.Orders.Add(order);
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    };

    private static async Task<List<CustomerActionAudit>> CustomerRows(CleansiaDbContext context) =>
        await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync();

    [Fact]
    public async Task A_Paid_Card_Order_Cancelled_Three_Hours_Before_An_Accepted_Job_Leaves_One_Success_Row_With_The_Fee_Figures()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: Seed(),
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new CancelOrder.Command(OrderId, Reason: "plans changed")),
            assert: async (CleansiaDbContext context, BusinessResult<CancelOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(0.50m, result.Value.FeeRate);

                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
                Assert.Equal(0.50m, order.CancellationFeeRate);

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.order.cancel", row.Action);
                Assert.True(row.Success);
                Assert.Null(row.ErrorCode);
                Assert.Equal(CustomerId, row.UserId);
                Assert.Equal("Order", row.ResourceType);
                Assert.Equal(OrderId, row.ResourceId);
                Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
                Assert.Equal(Ip, row.IpAddress);
                Assert.Equal(DeviceLabel, row.DeviceLabel);
                Assert.Equal(TestTenants.Default, row.TenantId);

                var payload = JsonDocument.Parse(row.PayloadJson!).RootElement;
                Assert.Equal("lastMinute", payload.GetProperty("tier").GetString());
                Assert.Equal(0.5m, payload.GetProperty("feeRate").GetDecimal());
                Assert.Equal(500m, payload.GetProperty("feeAmount").GetDecimal());
                Assert.Equal(500m, payload.GetProperty("refundAmount").GetDecimal());
                Assert.Equal(1000m, payload.GetProperty("totalPrice").GetDecimal());
                Assert.Equal(CurrencyId, payload.GetProperty("currencyId").GetString());
                Assert.True(payload.GetProperty("hasBeenAccepted").GetBoolean());
                Assert.True(payload.GetProperty("refundInitiated").GetBoolean());
                Assert.Equal(BookingPolicy.FreeCancellationHours, payload.GetProperty("freeCancellationHoursApplied").GetInt32());
                Assert.Equal(BookingPolicy.PartialCancellationFeeRate, payload.GetProperty("policyFigures").GetProperty("partialRate").GetDecimal());
                Assert.Equal("card", payload.GetProperty("paymentType").GetString());
                Assert.Equal("paid", payload.GetProperty("paymentStatus").GetString());
                Assert.True(payload.GetProperty("reasonProvided").GetBoolean());
                Assert.DoesNotContain("plans changed", row.PayloadJson);
                Assert.Equal(0, await context.AdminActionAudits.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task An_InProgress_Order_Leaves_One_Failure_Row_With_The_Key_The_Order_And_No_Payload()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: Seed(lastStatus: OrderStatus.InProgress),
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new CancelOrder.Command(OrderId, Reason: null)),
            assert: async (CleansiaDbContext context, BusinessResult<CancelOrder.Response> result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, result.Error!.Message);

                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(OrderStatus.InProgress, order.CurrentStatus);

                var row = Assert.Single(await CustomerRows(context));
                Assert.False(row.Success);
                Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, row.ErrorCode);
                Assert.Equal("customer.order.cancel", row.Action);
                Assert.Equal("Order", row.ResourceType);
                Assert.Equal(OrderId, row.ResourceId);
                Assert.Equal(CustomerId, row.UserId);
                Assert.Null(row.PayloadJson);
                Assert.Equal(Ip, row.IpAddress);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Probe_At_Another_Customers_Order_Leaves_The_Probers_Failure_Row_At_The_Victims_Order_And_The_Order_Untouched()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: Seed(ownerUserId: OtherCustomerId),
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new CancelOrder.Command(OrderId, Reason: null)),
            assert: async (CleansiaDbContext context, BusinessResult<CancelOrder.Response> result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);

                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
                Assert.Null(order.CancelledAt);
                Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);

                var row = Assert.Single(await CustomerRows(context));
                Assert.False(row.Success);
                Assert.Equal(BusinessErrorMessage.OrderNotFound, row.ErrorCode);
                Assert.Equal(CustomerId, row.UserId);
                Assert.Equal("Order", row.ResourceType);
                Assert.Equal(OrderId, row.ResourceId);
                Assert.Null(row.PayloadJson);
            },
            transactional: false);
    }

    private sealed class SucceedingRefunds : IRefundService
    {
        public Task<BusinessResult<RefundResult>> IssueRefundAsync(RefundRequest request, CancellationToken cancellationToken)
            => Task.FromResult(BusinessResult.Success(new RefundResult(
                "refund-cancel-audit", $"refund:{request.OrderId}:cancel", request.Amount, RefundStatus.Succeeded, false)));
    }
}
