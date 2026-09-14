using System.Security.Claims;
using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Auditing;

/// <summary>
/// ADR-0062 D6 on real Postgres, through the full MediatR pipeline: a user with two customer rows (the
/// booking of one order, a consent grant), one admin refund on that order and one cleaner drop of it. By
/// user the timeline returns the four rows <c>OccurredOn DESC</c> with the source of each; by resource it
/// returns the three that name the order and, unlike the user key, reaches a guest act; paging walks
/// the same order without a gap or a duplicate; a row stamped for another operator never appears
/// (the global tenant filter); and once the user is erased — the order no longer names them — the
/// admin and cleaner rows on it are still on their timeline, reached through their own booking row.
/// </summary>
[Collection("PostgresCollection")]
public class GetActionTimelineTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "CZ-timeline";
    private const string CurrencyId = "CZK-timeline";
    private const string CustomerId = "cust-timeline-1";
    private const string OrderId = "order-timeline-1";
    private const string GuestOrderId = "order-timeline-guest";

    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ByUser_Returns_The_Four_Rows_Newest_First_With_Their_Source()
    {
        await TestMethod(
            setup: AdminSession,
            arrange: SeedOneOrderWithFourActs,
            act: provider => Send(provider, new GetActionTimeline.Request { UserId = CustomerId }),
            assert: (CleansiaDbContext _, PagedData<TimelineEntryDto> page) =>
            {
                Assert.Equal(4, page.Total);
                Assert.Equal(
                    new[] { TimelineSource.Employee, TimelineSource.Admin, TimelineSource.Customer, TimelineSource.Customer },
                    page.Data.Select(e => e.Source));
                Assert.Equal(new[] { "employee.order.dropped", "order.refund.partial", "customer.consent.grant", "customer.order.create" },
                    page.Data.Select(e => e.Action));
                Assert.True(page.Data.Select(e => e.OccurredOn).SequenceEqual(page.Data.Select(e => e.OccurredOn).OrderByDescending(t => t)));
                Assert.Equal(new[] { OrderId, OrderId, CustomerId, OrderId }, page.Data.Select(e => e.ResourceId));
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task ByOrder_Returns_The_Three_Rows_Naming_It_And_Reaches_The_Guest_Act()
    {
        await TestMethod(
            setup: AdminSession,
            arrange: SeedOneOrderWithFourActs,
            act: async provider => (
                Order: await Send(provider, new GetActionTimeline.Request { ResourceType = "Order", ResourceId = OrderId }),
                Guest: await Send(provider, new GetActionTimeline.Request { ResourceType = "Order", ResourceId = GuestOrderId }),
                GuestByUser: await Send(provider, new GetActionTimeline.Request { UserId = CustomerId })),
            assert: (CleansiaDbContext _, (PagedData<TimelineEntryDto> Order, PagedData<TimelineEntryDto> Guest, PagedData<TimelineEntryDto> GuestByUser) r) =>
            {
                Assert.Equal(3, r.Order.Total);
                Assert.Equal(
                    new[] { TimelineSource.Employee, TimelineSource.Admin, TimelineSource.Customer },
                    r.Order.Data.Select(e => e.Source));

                var guest = Assert.Single(r.Guest.Data);
                Assert.Null(guest.ActorId);
                Assert.Equal(GuestOrderId, guest.ResourceId);

                Assert.DoesNotContain(r.GuestByUser.Data, e => e.ResourceId == GuestOrderId);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task Paging_Walks_The_Merged_Order_Without_A_Gap_Or_A_Duplicate()
    {
        await TestMethod(
            setup: AdminSession,
            arrange: SeedOneOrderWithFourActs,
            act: async provider =>
            {
                var pages = new List<PagedData<TimelineEntryDto>>();
                for (var offset = 0; offset < 4; offset += 3)
                {
                    pages.Add(await Send(provider, new GetActionTimeline.Request { UserId = CustomerId, Offset = offset, Limit = 3 }));
                }

                return pages;
            },
            assert: (CleansiaDbContext _, List<PagedData<TimelineEntryDto>> pages) =>
            {
                Assert.Equal(2, pages.Count);
                Assert.Equal(3, pages[0].Data.Count());
                Assert.Single(pages[1].Data);
                Assert.Equal(1, pages[0].PageNumber);
                Assert.Equal(2, pages[1].PageNumber);

                var ids = pages.SelectMany(p => p.Data).Select(e => e.Id).ToList();
                Assert.Equal(4, ids.Distinct().Count());
                Assert.Equal(
                    new[] { TimelineSource.Employee, TimelineSource.Admin, TimelineSource.Customer, TimelineSource.Customer },
                    pages.SelectMany(p => p.Data).Select(e => e.Source));
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task Another_Operators_Rows_Never_Appear()
    {
        await TestMethod(
            setup: AdminSession,
            arrange: async context =>
            {
                await SeedOneOrderWithFourActs(context);
                context.CustomerActionAudits.Add(CustomerRow("caud-other-tenant", CustomerId, OrderId, "customer.order.cancel", T0.AddHours(5), TestTenants.Second));
                context.AdminActionAudits.Add(AdminRow("aaud-other-tenant", OrderId, T0.AddHours(6), TestTenants.Second));
                context.EmployeeActionAudits.Add(EmployeeRow("eaud-other-tenant", OrderId, T0.AddHours(7), TestTenants.Second));
            },
            act: async provider => (
                ByUser: await Send(provider, new GetActionTimeline.Request { UserId = CustomerId }),
                ByOrder: await Send(provider, new GetActionTimeline.Request { ResourceType = "Order", ResourceId = OrderId })),
            assert: (CleansiaDbContext _, (PagedData<TimelineEntryDto> ByUser, PagedData<TimelineEntryDto> ByOrder) r) =>
            {
                Assert.Equal(4, r.ByUser.Total);
                Assert.Equal(3, r.ByOrder.Total);
                Assert.DoesNotContain(r.ByUser.Data.Concat(r.ByOrder.Data), e => e.Id.EndsWith("other-tenant"));
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task ByUser_Keeps_The_Admin_And_Cleaner_Rows_On_An_Erased_Subjects_Order()
    {
        await TestMethod(
            setup: AdminSession,
            arrange: context => SeedOneOrderWithFourActs(context, completed: true),
            act: async provider =>
            {
                var erased = await provider.GetRequiredService<IMediator>().Send(new AdminDeleteUserAccount.Command(CustomerId));
                Assert.True(erased.IsSuccess, erased.Error?.Message);

                return await Send(provider, new GetActionTimeline.Request { UserId = CustomerId });
            },
            assert: async (CleansiaDbContext context, PagedData<TimelineEntryDto> page) =>
            {
                Assert.Null(await context.Orders.IgnoreQueryFilters().Where(o => o.Id == OrderId).Select(o => o.UserId).SingleAsync());

                Assert.Equal(5, page.Total);
                Assert.Equal(
                    new[] { "gdpr.user.delete", "employee.order.dropped", "order.refund.partial", "customer.consent.grant", "customer.order.create" },
                    page.Data.Select(e => e.Action));
                Assert.Equal(
                    new[] { TimelineSource.Admin, TimelineSource.Employee, TimelineSource.Admin, TimelineSource.Customer, TimelineSource.Customer },
                    page.Data.Select(e => e.Source));
                Assert.Equal(new[] { CustomerId, OrderId, OrderId, CustomerId, OrderId }, page.Data.Select(e => e.ResourceId));
            },
            transactional: false);
    }

    private static async Task<PagedData<TimelineEntryDto>> Send(IServiceProvider provider, GetActionTimeline.Request request)
    {
        var mediator = provider.GetRequiredService<IMediator>();
        return await mediator.Send(request);
    }

    private static Task AdminSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            "admin-timeline",
            "admin-timeline@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    /// <summary>
    /// The customer books (T0) and grants a consent (T0+1h); an admin partially refunds the order (T0+2h);
    /// the cleaner who had it drops it (T0+3h). A guest booked a second order (T0+4h) that no user owns.
    /// A completed order is what lets the customer be erased — a live one refuses the erasure.
    /// </summary>
    private static Task SeedOneOrderWithFourActs(CleansiaDbContext context) => SeedOneOrderWithFourActs(context, completed: false);

    private static async Task SeedOneOrderWithFourActs(CleansiaDbContext context, bool completed)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var customer = User.CreateWithPassword("timeline-customer@cleansia.test", Constants.TestUserSession.TestUserPassword, "Time", "Line");
        customer.Id = CustomerId;
        customer.ConfirmEmail();
        context.Users.Add(customer);

        context.Orders.Add(NewOrder(OrderId, CustomerId, completed));
        context.Orders.Add(NewOrder(GuestOrderId, userId: null, completed));

        context.CustomerActionAudits.AddRange(
            CustomerRow("caud-create", CustomerId, OrderId, "customer.order.create", T0),
            CustomerRow("caud-consent", CustomerId, CustomerId, "customer.consent.grant", T0.AddHours(1), resourceType: "User"),
            CustomerRow("caud-guest", null, GuestOrderId, "customer.order.create", T0.AddHours(4)));
        context.AdminActionAudits.Add(AdminRow("aaud-refund", OrderId, T0.AddHours(2)));
        context.EmployeeActionAudits.Add(EmployeeRow("eaud-drop", OrderId, T0.AddHours(3)));

        // CommitAsync, not SaveChangesAsync: the reference rows and the order graph are Auditable and the
        // commit is what stamps CreatedBy/CreatedOn and the tenant on them.
        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(string orderId, string? userId, bool completed)
    {
        var order = Order.Create(
            customerName: "Time Line",
            customerEmail: "timeline-customer@cleansia.test",
            customerPhone: "+420777999888",
            customerAddress: Address.Create("Open St 1", "Brno", "60200", CountryId, latitude: 49.195060, longitude: 16.606837),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = orderId;
        order.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        var stamp = T0.AddDays(-2);
        foreach (var status in completed ? new[] { OrderStatus.New, OrderStatus.Completed } : [OrderStatus.New])
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created(Constants.TestUserSession.TestUserName, stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddDays(1);
        }

        return order;
    }

    private static CustomerActionAudit CustomerRow(
        string id, string? userId, string resourceId, string action, DateTimeOffset occurredOn,
        string tenantId = TestTenants.Default, string resourceType = "Order")
    {
        var row = CustomerActionAudit.Create(
            userId: userId, clientAudience: JwtAudiences.Customer, ipAddress: "203.0.113.9", deviceLabel: "Pixel 8",
            deviceId: "device-1", action: action, resourceType: resourceType, resourceId: resourceId, success: true, errorCode: null,
            payloadJson: "{\"feeRate\":0.5}", correlationId: null);
        row.Id = id;
        row.TenantId = tenantId;
        typeof(CustomerActionAudit).GetProperty(nameof(CustomerActionAudit.OccurredOn))!.SetValue(row, occurredOn);
        return row;
    }

    private static AdminActionAudit AdminRow(string id, string orderId, DateTimeOffset occurredOn, string tenantId = TestTenants.Default) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            ActorId = "admin-timeline",
            ActorEmail = "admin-timeline@cleansia.test",
            ActorProfile = UserProfile.Administrator,
            Action = "order.refund.partial",
            ResourceType = "Order",
            ResourceId = orderId,
            Success = true,
            OccurredOn = occurredOn,
        };

    private static EmployeeActionAudit EmployeeRow(string id, string orderId, DateTimeOffset occurredOn, string tenantId = TestTenants.Default)
    {
        var row = EmployeeActionAudit.Create("employee-timeline-1", orderId, EmployeeAuditAction.OrderDropped);
        row.Id = id;
        row.TenantId = tenantId;
        row.Created("employee-user-timeline-1", occurredOn);
        return row;
    }
}
