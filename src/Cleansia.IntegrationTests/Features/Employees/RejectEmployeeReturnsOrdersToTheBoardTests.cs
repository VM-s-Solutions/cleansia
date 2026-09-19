using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
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

namespace Cleansia.IntegrationTests.Features.Employees;

/// <summary>
/// A rejection through the real pipeline on real Postgres. The repository query the release walks is
/// what this proves: the walk-back appends a status row, and a row appended to a graph loaded without
/// its history collides with the creation row's <c>Sequence</c> — so the <c>New</c> row must land with
/// the NEXT sequence, on every emptied order, in one commit with the contract change. The company's
/// administrators each get one <c>admin.order.crew_lost</c> row per emptied order; another company's
/// administrator gets none; a live reservation held by the rejected cleaner ends and one held by
/// somebody else does not; a crew that remains keeps the status.
/// </summary>
[Collection("PostgresCollection")]
public class RejectEmployeeReturnsOrdersToTheBoardTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AdminA1 = "admin-reject-board-a1";
    private const string AdminA2 = "admin-reject-board-a2";
    private const string AdminB1 = "admin-reject-board-b1";
    private const string AdminA1Email = "admin-reject-board-a1@cleansia.test";
    private const string CustomerId = "user-reject-board-cust";
    private const string CleanerId = "emp-reject-board-1";
    private const string ColleagueId = "emp-reject-board-2";
    private const string OtherPreferredId = "emp-reject-board-other";
    private const string HeldOrderId = "order-reject-board-held";
    private const string OtherHoldOrderId = "order-reject-board-other-hold";
    private const string CrewedOrderId = "order-reject-board-crewed";
    private const string CurrencyId = "currency-czk-reject-board";
    private const string CountryId = "country-cz-reject-board";

    private static Task AdminSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            AdminA1, AdminA1Email, [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    private static User Administrator(string id, string tenantId)
    {
        var user = User.CreateWithPassword($"{id}@cleansia.test", "Seed-Password-123", "Ad", "Min", UserProfile.Administrator);
        user.Id = id;
        user.TenantId = tenantId;
        user.ConfirmEmail();
        return user;
    }

    private static Employee Cleaner(string id)
    {
        var user = User.CreateWithPassword($"{id}@cleansia.test", "Seed-Password-123", "Emp", "Loyee", UserProfile.Employee);
        user.Id = $"{id}-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = id;
        employee.UpdateContractStatus(ContractStatus.Approved);
        return employee;
    }

    private static Order ConfirmedOrder(string id, int maxEmployees, string? heldFor, params Employee[] crew)
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "reject-board@cleansia.test",
            customerPhone: "+420777111333",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1250m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerId);
        order.Id = id;
        order.SetMaxEmployees(maxEmployees);
        var stamp = DateTimeOffset.UtcNow.AddDays(-1);
        foreach (var status in new[] { OrderStatus.New, OrderStatus.Confirmed })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("seed", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        if (heldFor is not null)
        {
            var now = DateTime.UtcNow;
            order.GrantPreferredHold(heldFor, now.AddHours(12), now, maxRounds: 3);
        }

        foreach (var employee in crew)
        {
            order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
        }

        return order;
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

        var customer = User.CreateWithPassword("reject-board@cleansia.test", "Seed-Password-123", "Jana", "Nováková");
        customer.Id = CustomerId;
        context.Users.Add(customer);
        context.Users.AddRange(
            Administrator(AdminA1, TestTenants.Default),
            Administrator(AdminA2, TestTenants.Default),
            Administrator(AdminB1, TestTenants.Second));

        var cleaner = Cleaner(CleanerId);
        var colleague = Cleaner(ColleagueId);
        context.Employees.AddRange(cleaner, colleague);

        context.Orders.AddRange(
            ConfirmedOrder(HeldOrderId, maxEmployees: 1, heldFor: CleanerId, cleaner),
            ConfirmedOrder(OtherHoldOrderId, maxEmployees: 1, heldFor: OtherPreferredId, cleaner),
            ConfirmedOrder(CrewedOrderId, maxEmployees: 2, heldFor: null, cleaner, colleague));
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Every_Emptied_Order_Lands_At_New_With_The_Next_Sequence_And_Each_Administrator_Is_Told_Once_Per_Order()
    {
        var before = DateTime.UtcNow;
        await TestMethod(
            setup: AdminSession,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new RejectEmployee.Command(CleanerId, "documents look forged")),
            assert: async (CleansiaDbContext context, BusinessResult<RejectEmployee.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var orders = await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.OrderStatusHistory)
                    .Include(o => o.AssignedEmployees)
                    .Where(o => o.Id == HeldOrderId || o.Id == OtherHoldOrderId || o.Id == CrewedOrderId)
                    .AsSplitQuery()
                    .ToDictionaryAsync(o => o.Id);

                foreach (var emptied in new[] { orders[HeldOrderId], orders[OtherHoldOrderId] })
                {
                    Assert.Empty(emptied.AssignedEmployees);
                    Assert.Equal(OrderStatus.New, emptied.CurrentStatus);
                    Assert.Equal([0, 1, 2], emptied.OrderStatusHistory.OrderBy(s => s.Sequence).Select(s => s.Sequence));
                    Assert.Equal(OrderStatus.New, emptied.OrderStatusHistory.Single(s => s.Sequence == 2).Status);
                }

                Assert.True(orders[HeldOrderId].PreferredHoldUntilUtc <= DateTime.UtcNow);
                Assert.Equal(OtherPreferredId, orders[OtherHoldOrderId].PreferredEmployeeId);
                Assert.True(orders[OtherHoldOrderId].PreferredHoldUntilUtc > before.AddHours(11));

                var crewed = orders[CrewedOrderId];
                Assert.Equal(ColleagueId, Assert.Single(crewed.AssignedEmployees).EmployeeId);
                Assert.Equal(OrderStatus.Confirmed, crewed.CurrentStatus);
                Assert.Equal([0, 1, 2], crewed.OrderStatusHistory.OrderBy(s => s.Sequence).Select(s => s.Sequence));
                Assert.Equal(OrderStatus.Confirmed, crewed.OrderStatusHistory.Single(s => s.Sequence == 2).Status);

                var rows = await context.Set<UserNotification>().IgnoreQueryFilters()
                    .Where(n => n.EventKey == AdminNotificationEventCatalog.OrderCrewLost)
                    .ToListAsync();
                Assert.Equal(4, rows.Count);
                Assert.All(rows, row => Assert.Equal(TestTenants.Default, row.TenantId));
                Assert.DoesNotContain(rows, r => r.UserId == AdminB1);
                foreach (var admin in new[] { AdminA1, AdminA2 })
                {
                    var orderIds = rows
                        .Where(r => r.UserId == admin)
                        .Select(r => JsonSerializer.Deserialize<Dictionary<string, string>>(r.ArgsJson)!)
                        .Select(args =>
                        {
                            Assert.Equal("rejected", args["cause"]);
                            Assert.Equal(nameof(OrderStatus.Confirmed), args["statusAtLoss"]);
                            return args["orderId"];
                        })
                        .OrderBy(id => id);
                    Assert.Equal([HeldOrderId, OtherHoldOrderId], orderIds);
                }
            },
            transactional: false);
    }
}
