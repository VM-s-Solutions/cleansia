using System.Security.Claims;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Features.Auditing.Filters;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Auditing;

/// <summary>
/// Q-AUD-O2 (owner ruling) on real Postgres through the real pipeline: an admin refused a cancellation
/// on an in-progress order leaves one out-of-band admin failure row that names the ORDER and the KEY,
/// and both admin reads find it by that order — the timeline by resource and the admin list filtered
/// by resource id. Without a resource type on the row neither read would; the reason alone is worth
/// nothing.
/// </summary>
[Collection("PostgresCollection")]
public class AdminRefusalTraceabilityTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AdminId = "admin-refusal-trace";
    private const string CustomerId = "cust-refusal-trace";
    private const string OrderId = "order-refusal-trace";
    private const string OtherOrderId = "order-refusal-trace-other";
    private const string CurrencyId = "currency-czk-refusal-trace";
    private const string CountryId = "country-cz-refusal-trace";

    private static Task AdminSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            AdminId, "admin-trace@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    private static async Task Seed(CleansiaDbContext context)
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

        var customer = User.CreateWithPassword("refusal-trace@cleansia.test", "Seed-Password-123", "Trace", "Able");
        customer.Id = CustomerId;
        context.Users.Add(customer);

        context.Orders.Add(NewOrder(OrderId, OrderStatus.InProgress));
        context.Orders.Add(NewOrder(OtherOrderId, OrderStatus.Confirmed));

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(string id, OrderStatus lastStatus)
    {
        var order = Order.Create(
            customerName: "Trace Able",
            customerEmail: "refusal-trace@cleansia.test",
            customerPhone: "+420777111222",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(3),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: CustomerId);
        order.Id = id;
        order.Created("seed", DateTimeOffset.UtcNow.AddDays(-2));
        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var status in new[] { OrderStatus.New, lastStatus })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("seed", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        return order;
    }

    private sealed record Observed(
        BusinessResult<AdminCancelOrder.Response> Refusal,
        PagedData<TimelineEntryDto> TimelineByOrder,
        PagedData<TimelineEntryDto> TimelineByOtherOrder,
        PagedData<AdminActionAuditDto> AdminListByOrder);

    [Fact]
    public async Task An_Admin_Refused_On_An_InProgress_Order_Leaves_A_Failure_Row_That_Both_Admin_Reads_Find_By_The_Order()
    {
        await TestMethod(
            setup: AdminSession,
            arrange: Seed,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var refusal = await mediator.Send(new AdminCancelOrder.Command(OrderId, Reason: "support asked"));
                return new Observed(
                    refusal,
                    await mediator.Send(new GetActionTimeline.Request { ResourceType = "Order", ResourceId = OrderId }),
                    await mediator.Send(new GetActionTimeline.Request { ResourceType = "Order", ResourceId = OtherOrderId }),
                    await mediator.Send(new GetPagedAdminActionAudits.Request { Filter = new AdminActionAuditFilter(null, null, null, null, OrderId, null, null, null) }));
            },
            assert: async (CleansiaDbContext context, Observed observed) =>
            {
                Assert.True(observed.Refusal.IsFailure);
                Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, observed.Refusal.Error!.Message);

                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(OrderStatus.InProgress, order.CurrentStatus);

                var row = Assert.Single(await context.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
                Assert.False(row.Success);
                Assert.Equal("order.cancel", row.Action);
                Assert.Equal("Order", row.ResourceType);
                Assert.Equal(OrderId, row.ResourceId);
                Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, row.ErrorCode);
                Assert.Equal(AdminId, row.ActorId);
                Assert.Equal(TestTenants.Default, row.TenantId);
                Assert.Null(row.AfterJson);
                Assert.DoesNotContain("support asked", row.Reason ?? string.Empty);

                var entry = Assert.Single(observed.TimelineByOrder.Data);
                Assert.Equal(TimelineSource.Admin, entry.Source);
                Assert.Equal(row.Id, entry.Id);
                Assert.Equal(AdminId, entry.ActorId);
                Assert.Equal("order.cancel", entry.Action);
                Assert.Equal(OrderId, entry.ResourceId);
                Assert.False(entry.Success);
                Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, entry.ErrorCode);

                Assert.Empty(observed.TimelineByOtherOrder.Data);

                var listed = Assert.Single(observed.AdminListByOrder.Data);
                Assert.Equal(row.Id, listed.Id);
                Assert.Equal(OrderId, listed.ResourceId);
                Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, listed.ErrorCode);
            },
            transactional: false);
    }
}
