using System.Security.Claims;
using System.Globalization;
using SortDefinition = Cleansia.Core.AppServices.Shared.DTOs.Sorting.SortDefinition;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Orders.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// TC-BE-ORDERS-GETPAGED-SCOPE (T-0339) — the GetPaged "mine" views are pinned to the JWT caller, driven
/// END-TO-END through the real host (<c>IMediator.Send(GetPagedOrders.Request)</c> → spec → repo) over a
/// REAL Postgres (Testcontainers). Closes the T-0307 security gate (DECISION 2b): a non-admin employee
/// must not be able to over-read another employee's exclusive (assigned, no-spot) rows by supplying a
/// foreign <c>Filter.EmployeeId</c>.
///
/// The leak the spec fix removes: the per-row blank only hid customer name/email/phone/street; for a
/// FOREIGN-assigned row it still emitted the exact coordinates, the confirmation code, and the assignee's
/// pay. The server now pins the caller's id (non-admin cannot filter by a foreign employee) and constrains
/// the base query to "assigned-to-caller OR still-takeable", so a foreign exclusive row is never returned.
/// </summary>
[Collection("PostgresCollection")]
public class GetPagedOrdersScopeIntegrationTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    // Entity Ids are varchar(26) (ULID length) — every seeded Id must be <= 26 chars.
    private const string CurrencyId = "currency-czk-gpscope";
    private const string CountryId = "country-cz-gpscope";

    private const string EmployeeAId = "employee-a-gpscope";
    private const string EmployeeBId = "employee-b-gpscope";
    private const string UserAId = "user-a-gpscope";
    private const string UserBId = "user-b-gpscope";

    private const string EmployeeAEmail = "employee-a-getpaged@cleansia.test";
    private const string EmployeeBEmail = "employee-b-getpaged@cleansia.test";

    private const string AssignedToBOrderId = "order-assigned-b-gpscope";
    private const string AssignedToAOrderId = "order-assigned-a-gpscope";
    private const string AvailableOrderId = "order-available-gpscope";

    private const double SecretLatitude = 50.073658;
    private const double SecretLongitude = 14.418540;

    // ── The exploit: employee A passes Filter.EmployeeId=B + Confirmed/InProgress/Completed and must get
    //    NONE of B's exclusive assigned-order rows (no coords / confirmation code / pay leak). ──

    [Fact]
    public async Task NonAdmin_PassingForeignEmployeeId_DoesNotReceiveForeignAssignedRows()
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services, UserAId, EmployeeAEmail, EmployeeAId),
            arrange: SeedTwoEmployeesWithOrders,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(MineRequest(employeeIdFilter: EmployeeBId));
            },
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                var rows = page.Data!.ToList();

                // The whole point: B's exclusive (assigned-to-B, no-spot) order never appears for A.
                Assert.DoesNotContain(rows, r => r.Id == AssignedToBOrderId);

                // And the leak channel is gone — no row carries B's coordinates.
                Assert.DoesNotContain(rows, r =>
                    r.CustomerAddressLatitude == SecretLatitude || r.CustomerAddressLongitude == SecretLongitude);

                // A still sees its own assigned row (the foreign id filter was overridden to A's own id,
                // and A's order matches Confirmed/InProgress/Completed).
                Assert.Contains(rows, r => r.Id == AssignedToAOrderId);

                return Task.CompletedTask;
            });
    }

    // ── No-regression: A's legitimate "mine" call (Filter.EmployeeId=A) still returns A's assigned rows. ──

    [Fact]
    public async Task NonAdmin_RequestingOwnEmployeeId_StillReceivesOwnAssignedRows()
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services, UserAId, EmployeeAEmail, EmployeeAId),
            arrange: SeedTwoEmployeesWithOrders,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(MineRequest(employeeIdFilter: EmployeeAId));
            },
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                var rows = page.Data!.ToList();
                Assert.Contains(rows, r => r.Id == AssignedToAOrderId);
                Assert.DoesNotContain(rows, r => r.Id == AssignedToBOrderId);
                return Task.CompletedTask;
            });
    }

    // ── No-regression: the Available/unassigned pane still returns the takeable row, and never a
    //    foreign-assigned, no-spot row. The takeable row's exact coords + confirmation code stay blanked
    //    pre-accept. ──

    [Fact]
    public async Task NonAdmin_BrowsingAvailable_ReturnsTakeableRow_NotForeignAssigned()
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services, UserAId, EmployeeAEmail, EmployeeAId),
            arrange: SeedTwoEmployeesWithOrders,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(AvailableRequest());
            },
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                var rows = page.Data!.ToList();

                var available = Assert.Single(rows, r => r.Id == AvailableOrderId);
                Assert.DoesNotContain(rows, r => r.Id == AssignedToBOrderId);

                // Pre-accept: full PII + exact coords hidden; coarse area still shown.
                Assert.Equal(string.Empty, available.CustomerName);
                Assert.Null(available.CustomerAddressLatitude);
                Assert.Null(available.CustomerAddressLongitude);
                Assert.NotEqual(string.Empty, available.CustomerAddressApproximate);

                return Task.CompletedTask;
            });
    }

    // ── Admin is preserved: the broad cross-employee filter still resolves B's assigned row. ──

    [Fact]
    public async Task Admin_PassingEmployeeId_StillReceivesThatEmployeesAssignedRows()
    {
        await TestMethod(
            setup: ReplaceWithAdminSession,
            arrange: SeedTwoEmployeesWithOrders,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(MineRequest(employeeIdFilter: EmployeeBId));
            },
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                var rows = page.Data!.ToList();
                var bRow = Assert.Single(rows, r => r.Id == AssignedToBOrderId);
                // Admin keeps the full read — its policy already permits cross-employee access.
                Assert.Equal(SecretLatitude, bRow.CustomerAddressLatitude);
                return Task.CompletedTask;
            });
    }

    // ── Owner ruling 2026-09-28 (D67): the crew's access to the customer ends 24 h after completion and at
    //    once on cancellation, on the list as on the detail. ──

    private const string CompletedLongAgoOrderId = "order-done-25h-gpscope";
    private const string CompletedRecentlyOrderId = "order-done-23h-gpscope";
    private const string CancelledOrderId = "order-cancelled-gpscope";

    [Fact]
    public async Task Past_The_Window_The_Assigned_Cleaners_Row_Carries_No_Customer()
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services, UserAId, EmployeeAEmail, EmployeeAId),
            arrange: SeedPastJobsOfEmployeeA,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new GetPagedOrders.Request
                {
                    Filter = EmptyFilter() with
                    {
                        EmployeeId = EmployeeAId,
                        OrderStatuses = new[] { OrderStatus.Completed, OrderStatus.Cancelled },
                    },
                });
            },
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                var rows = page.Data!.ToList();

                foreach (var closed in new[] { CompletedLongAgoOrderId, CancelledOrderId })
                {
                    var row = Assert.Single(rows, r => r.Id == closed);
                    Assert.Equal(string.Empty, row.CustomerName);
                    Assert.Equal(string.Empty, row.CustomerPhone);
                    Assert.Null(row.CustomerAddressLatitude);
                    Assert.Null(row.CustomerAddressLongitude);
                }

                var open = Assert.Single(rows, r => r.Id == CompletedRecentlyOrderId);
                Assert.Equal("Scope Customer", open.CustomerName);
                Assert.Equal("+420777999888", open.CustomerPhone);
                Assert.NotNull(open.CustomerAddressLatitude);

                return Task.CompletedTask;
            });
    }

    [Theory]
    [InlineData("CustomerName", "en-US")]
    [InlineData("CustomerEmail", "en-US")]
    [InlineData("CustomerEmail", "tr-TR")]
    public async Task Open_Board_Drops_All_Contact_Sorts_Before_Selecting_The_Page(string field, string culture)
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services, UserAId, EmployeeAEmail, EmployeeAId),
            arrange: SeedContactSortOrders,
            act: async provider =>
            {
                var previousCulture = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    var mediator = provider.GetRequiredService<IMediator>();
                    var baseline = await mediator.Send(SortRequest([], limit: 1));
                    var safe = await mediator.Send(SortRequest([new("CleaningDateTime")], limit: 2));
                    var ascending = await mediator.Send(SortRequest([new(field.ToUpper(CultureInfo.CurrentCulture))], limit: 1));
                    var descending = await mediator.Send(SortRequest([new(field, SortDirection.Descending)], limit: 1));
                    var multiple = await mediator.Send(SortRequest([
                        new("CleaningDateTime"), new("CustomerName"), new("CustomerEmail", SortDirection.Descending)], limit: 2));
                    return (Baseline: baseline, Safe: safe, Ascending: ascending, Descending: descending, Multiple: multiple);
                }
                finally
                {
                    CultureInfo.CurrentCulture = previousCulture;
                }
            },
            assert: (CleansiaDbContext _, (PagedData<OrderListItem> Baseline, PagedData<OrderListItem> Safe,
                PagedData<OrderListItem> Ascending, PagedData<OrderListItem> Descending, PagedData<OrderListItem> Multiple) pages) =>
            {
                Assert.Equal(pages.Baseline.Data!.Select(o => o.Id), pages.Ascending.Data!.Select(o => o.Id));
                Assert.Equal(pages.Baseline.Data!.Select(o => o.Id), pages.Descending.Data!.Select(o => o.Id));
                Assert.Equal(pages.Safe.Data!.Select(o => o.Id), pages.Multiple.Data!.Select(o => o.Id));
                foreach (var page in new[] { pages.Ascending, pages.Descending, pages.Multiple })
                {
                    Assert.All(page.Data!, row =>
                    {
                        Assert.Equal(string.Empty, row.CustomerName);
                        Assert.Equal(string.Empty, row.CustomerEmail);
                    });
                }
                return Task.CompletedTask;
            });
    }

    [Theory]
    [InlineData("CustomerName", false)]
    [InlineData("CustomerEmail", false)]
    [InlineData("CustomerName", true)]
    [InlineData("CustomerEmail", true)]
    public async Task Admin_And_Own_Rows_Retain_Contact_Sorting(string field, bool ownRows)
    {
        await TestMethod(
            setup: services => ownRows
                ? ReplaceWithEmployeeSession(services, UserAId, EmployeeAEmail, EmployeeAId)
                : ReplaceWithAdminSession(services),
            arrange: async context =>
            {
                await SeedContactSortOrders(context);
                if (ownRows)
                {
                    var employee = (await context.Employees.FindAsync(EmployeeAId))!;
                    foreach (var id in new[] { "order-sort-a", "order-sort-z" })
                    {
                        var order = (await context.Orders.FindAsync(id))!;
                        order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
                        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
                    }
                    await context.CommitAsync(CancellationToken.None);
                }
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var filter = ownRows
                    ? EmptyFilter() with { EmployeeId = EmployeeBId, OrderStatuses = [OrderStatus.Cancelled] }
                    : EmptyFilter() with { IsUnassigned = true };
                var ascending = await mediator.Send(new GetPagedOrders.Request { Filter = filter, Sort = [new(field)] });
                var descending = await mediator.Send(new GetPagedOrders.Request { Filter = filter, Sort = [new(field, SortDirection.Descending)] });
                return (Ascending: ascending, Descending: descending);
            },
            assert: (CleansiaDbContext _, (PagedData<OrderListItem> Ascending, PagedData<OrderListItem> Descending) pages) =>
            {
                var expected = ownRows
                    ? new[] { "order-sort-a", "order-sort-z" }
                    : new[] { "order-sort-a", AvailableOrderId, "order-sort-z" };
                Assert.Equal(expected, pages.Ascending.Data!.Select(o => o.Id));
                Assert.Equal(expected.Reverse(), pages.Descending.Data!.Select(o => o.Id));
                if (ownRows)
                {
                    Assert.All(pages.Ascending.Data!, row => Assert.Equal(string.Empty, row.CustomerEmail));
                }
                return Task.CompletedTask;
            });
    }

    [Theory]
    [InlineData("CustomerPhone")]
    [InlineData("CustomerAddress")]
    [InlineData("Address")]
    [InlineData("SpecificPlace")]
    public async Task Unsupported_Contact_Keys_Keep_The_Existing_CreatedOn_Fallback(string field)
    {
        await TestMethod(
            setup: services => ReplaceWithEmployeeSession(services, UserAId, EmployeeAEmail, EmployeeAId),
            arrange: SeedContactSortOrders,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                foreach (var direction in new[] { SortDirection.Ascending, SortDirection.Descending })
                {
                    var baseline = await mediator.Send(SortRequest([new("CreatedOn", direction)], 2));
                    var page = await mediator.Send(SortRequest([new(field, direction)], 2));
                    Assert.Equal(baseline.Data!.Select(o => o.Id), page.Data!.Select(o => o.Id));
                }
                return true;
            },
            assert: (CleansiaDbContext _, bool passed) => { Assert.True(passed); return Task.CompletedTask; });
    }

    private static GetPagedOrders.Request SortRequest(SortDefinition[] sort, int limit) =>
        new() { Filter = EmptyFilter() with { IsUnassigned = true }, Sort = sort, Limit = limit };

    private static async Task SeedContactSortOrders(CleansiaDbContext context)
    {
        await SeedTwoEmployeesWithOrders(context);
        var slot = DateTime.UtcNow.AddDays(2);
        var first = CreateOrder("order-sort-a", OrderStatus.New, false, "Alpha", "a@cleansia.test", slot);
        first.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow.AddHours(-3));
        var last = CreateOrder("order-sort-z", OrderStatus.New, false, "Zulu", "z@cleansia.test", slot);
        last.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow.AddHours(-2));
        context.AddRange(first, last);
        await context.CommitAsync(CancellationToken.None);
    }

    private static async Task SeedPastJobsOfEmployeeA(CleansiaDbContext context)
    {
        await SeedTwoEmployeesWithOrders(context);
        var employeeA = await context.Employees.FindAsync(EmployeeAId);

        foreach (var (orderId, status, completedHoursAgo) in new (string, OrderStatus, int?)[]
                 {
                     (CompletedLongAgoOrderId, OrderStatus.Completed, 25),
                     (CompletedRecentlyOrderId, OrderStatus.Completed, 23),
                     (CancelledOrderId, OrderStatus.Cancelled, null),
                 })
        {
            var order = CreateOrder(orderId, status, withSecretCoords: false);
            order.AddAssignedEmployee(OrderEmployee.Create(order, employeeA!));
            if (completedHoursAgo is { } hours)
            {
                order.MarkCompletedAt(DateTime.UtcNow.AddHours(-hours));
            }

            context.Add(order);
        }

        await context.CommitAsync(CancellationToken.None);
    }

    private static GetPagedOrders.Request MineRequest(string employeeIdFilter) =>
        new()
        {
            Filter = EmptyFilter() with
            {
                EmployeeId = employeeIdFilter,
                OrderStatuses = new[] { OrderStatus.Confirmed, OrderStatus.InProgress, OrderStatus.Completed },
            },
        };

    private static GetPagedOrders.Request AvailableRequest() =>
        new()
        {
            Filter = EmptyFilter() with
            {
                IsUnassigned = true,
                OrderStatuses = new[] { OrderStatus.New, OrderStatus.Confirmed },
            },
        };

    private static OrderFilter EmptyFilter() => new(
        Id: null,
        IsActive: null,
        CustomerName: null,
        CustomerEmail: null,
        CustomerPhone: null,
        DisplayOrderNumber: null,
        EmployeeId: null,
        CleaningDateFrom: null,
        CleaningDateTo: null,
        PaymentStatuses: null,
        PaymentTypes: null,
        MinTotalPrice: null,
        MaxTotalPrice: null,
        OrderStatuses: null,
        HasAvailableSpots: null,
        IsUnassigned: null,
        ExcludeEmployeeId: null);

    private static Task ReplaceWithEmployeeSession(IServiceCollection services, string userId, string email, string employeeId)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            userId,
            email,
            [
                new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
                new Claim(TestUserSessionProvider.EmployeeIdClaimType, employeeId),
            ])));
        return Task.CompletedTask;
    }

    private static Task ReplaceWithAdminSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            "admin-getpaged-scope",
            "admin-getpaged@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    private static async Task SeedTwoEmployeesWithOrders(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        context.CountryConfigurations.Add(CountryConfiguration.Create(CountryId, "CZK", "cs", 0.21m));

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var employeeA = CreateApprovedEmployee(UserAId, EmployeeAId, EmployeeAEmail, "Anna", "Aslan");
        var employeeB = CreateApprovedEmployee(UserBId, EmployeeBId, EmployeeBEmail, "Bohdan", "Bilek");
        context.Add(employeeA);
        context.Add(employeeB);

        // B's exclusive, fully-assigned (no-spot) order with the confidential coords + confirmation code.
        var assignedToB = CreateOrder(AssignedToBOrderId, OrderStatus.Confirmed, withSecretCoords: true);
        assignedToB.AddAssignedEmployee(OrderEmployee.Create(assignedToB, employeeB));
        context.Add(assignedToB);

        // A's own assigned (no-spot) order — A's legitimate "mine" result.
        var assignedToA = CreateOrder(AssignedToAOrderId, OrderStatus.InProgress, withSecretCoords: false);
        assignedToA.AddAssignedEmployee(OrderEmployee.Create(assignedToA, employeeA));
        context.Add(assignedToA);

        // A genuinely-unassigned, takeable order — the Available pane.
        var available = CreateOrder(AvailableOrderId, OrderStatus.Confirmed, withSecretCoords: false);
        context.Add(available);

        await context.CommitAsync(CancellationToken.None);
    }

    private static Employee CreateApprovedEmployee(string userId, string employeeId, string email, string first, string last)
    {
        var user = User.CreateWithPassword(email, Constants.TestUserSession.TestUserPassword, first, last, UserProfile.Employee);
        user.Id = userId;
        user.ConfirmEmail();
        user.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.AssignWorkCountry(CountryId);
        employee.Approve(approvedByUserId: "admin-getpaged-scope");
        employee.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        return employee;
    }

    private static Order CreateOrder(string orderId, OrderStatus status, bool withSecretCoords,
        string customerName = "Scope Customer", string customerEmail = "scope-customer@cleansia.test", DateTime? cleaningDateTime = null)
    {
        // Every order carries real coordinates so no background geocode fires; the
        // takeable-row assertion then proves the handler BLANKED a real value to null
        // pre-accept, not that one was simply never set.
        var address = withSecretCoords
            ? Address.Create("Secret St 7", "Praha", "14000", CountryId, latitude: SecretLatitude, longitude: SecretLongitude)
            : Address.Create("Open St 1", "Brno", "60200", CountryId, latitude: 49.195060, longitude: 16.606837);

        var order = Order.Create(
            customerName: customerName,
            customerEmail: customerEmail,
            customerPhone: "+420777999888",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime ?? DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = orderId;
        order.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        return order;
    }
}
