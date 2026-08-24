using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The two sweep queries this branch added, against a REAL Postgres.
///
/// <para><b>Why they cannot be left to the unit suite.</b> Both are exercised there through
/// <c>MockQueryable</c>, which is LINQ-to-Objects: it happily runs a shape EF Core cannot translate to
/// SQL, so a green unit test says nothing about whether the query works. The digest's batched count is
/// a <c>SelectMany</c> into a <c>GroupBy</c> with an anonymous projection — precisely the shape that
/// falls back to client evaluation or throws — and <c>GetFutureConfirmedOrdersForEmployeeAsync</c>
/// filters on a collection navigation inside a <c>Where</c>. Only bytes in Postgres can answer.</para>
/// </summary>
[Collection("PostgresCollection")]
public class SweepQueryTranslationPostgresTests(PostgresContainerFixture fixture)
    : BaseIntegrationTest(fixture)
{
    private const string HeldEmployeeId = "emp-holds-work";
    private const string OtherEmployeeId = "emp-holds-nothing";

    /// <summary>
    /// Future <c>Confirmed</c> work only. The past order, the in-progress one and the other cleaner's
    /// are all seeded so the filter has something to be wrong about.
    /// </summary>
    [Fact]
    public async Task Future_Confirmed_Work_For_One_Cleaner_Translates_And_Filters()
    {
        await TestMethod(
            arrange: async context => await SeedAsync(context),
            act: async provider =>
            {
                var orders = provider.GetRequiredService<IOrderRepository>();
                return await orders.GetFutureConfirmedOrdersForEmployeeAsync(
                    HeldEmployeeId, DateTime.UtcNow, CancellationToken.None);
            },
            assert: (_, result) =>
            {
                // Two future Confirmed orders; the past one, the InProgress one and the other
                // cleaner's are all excluded.
                Assert.Equal(2, result.Count);
                Assert.All(result, o => Assert.Equal(OrderStatus.Confirmed, o.CurrentStatus));
                Assert.All(result, o => Assert.True(o.CleaningDateTime > DateTime.UtcNow));
                Assert.All(result, o =>
                    Assert.Contains(o.AssignedEmployees, a => a.EmployeeId == HeldEmployeeId));

                // The caller notifies the cleaner whose seat it takes, so the navigation must be
                // populated — a null Employee here would silently drop that notice.
                Assert.All(result, o => Assert.All(o.AssignedEmployees, a => Assert.NotNull(a.Employee)));
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The digest's batched count, run as the handler builds it. This is the translation test: the
    /// unit suite's mock would pass on a shape Postgres rejects.
    /// </summary>
    [Fact]
    public async Task The_Batched_Per_Cleaner_Count_Translates_To_Sql()
    {
        await TestMethod(
            arrange: async context => await SeedAsync(context),
            act: async provider =>
            {
                var orders = provider.GetRequiredService<IOrderRepository>();
                var ids = new List<string> { HeldEmployeeId, OtherEmployeeId };
                var start = DateTime.UtcNow;
                var end = DateTime.UtcNow.AddDays(30);

                return await orders.GetQueryableIgnoringTenant()
                    .Where(o => o.CurrentStatus == OrderStatus.Confirmed
                        && o.CleaningDateTime >= start
                        && o.CleaningDateTime < end)
                    .SelectMany(o => o.AssignedEmployees)
                    .Where(ae => ids.Contains(ae.EmployeeId))
                    .GroupBy(ae => ae.EmployeeId)
                    .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                    .ToListAsync(CancellationToken.None);
            },
            assert: (_, result) =>
            {
                var held = Assert.Single(result, r => r.EmployeeId == HeldEmployeeId);
                Assert.Equal(2, held.Count);

                // A cleaner with no jobs is ABSENT rather than zero — the handler reads that as
                // "nothing to say", which is the ordinary evening.
                Assert.DoesNotContain(result, r => r.EmployeeId == OtherEmployeeId);
                return Task.CompletedTask;
            });
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        // Users carry a PreferredLanguageCode FK, so the row has to exist before any user does.
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = Ulid.NewUlid().ToString();
        var currency = Currency.Create("CZK", "Kc", "Czech koruna", 1.0m);
        currency.Id = Ulid.NewUlid().ToString();
        context.Countries.Add(country);
        context.Currencies.Add(currency);

        var held = NewEmployee(HeldEmployeeId);
        var other = NewEmployee(OtherEmployeeId);
        context.Employees.Add(held);
        context.Employees.Add(other);

        // Two future Confirmed orders for the cleaner under test — the rows both queries must find.
        context.Orders.Add(NewOrder(
            "order-future-1", country.Id, currency.Id, DateTime.UtcNow.AddDays(2),
            OrderStatus.Confirmed, held));
        context.Orders.Add(NewOrder(
            "order-future-2", country.Id, currency.Id, DateTime.UtcNow.AddDays(3),
            OrderStatus.Confirmed, held));

        // Past, so out of both windows.
        context.Orders.Add(NewOrder(
            "order-past", country.Id, currency.Id, DateTime.UtcNow.AddDays(-2),
            OrderStatus.Confirmed, held));

        // In progress: a cleaner standing in someone's home. Deliberately not released.
        context.Orders.Add(NewOrder(
            "order-in-progress", country.Id, currency.Id, DateTime.UtcNow.AddHours(1),
            OrderStatus.InProgress, held));

        await context.CommitAsync(CancellationToken.None);
    }

    private static Employee NewEmployee(string employeeId)
    {
        var user = User.CreateWithPassword($"{employeeId}@example.com", "12345678Test!", "Emp", "Loyee");
        user.Id = Ulid.NewUlid().ToString();
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        return employee;
    }

    private static Order NewOrder(
        string orderId,
        string countryId,
        string currencyId,
        DateTime cleaningDateTime,
        OrderStatus status,
        Employee assignee)
    {
        var order = Order.Create(
            customerName: "Sweep Customer",
            customerEmail: $"{orderId}@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", countryId),
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: currencyId,
            paymentStatus: PaymentStatus.Pending);
        order.Id = orderId;
        order.SetMaxEmployees(1);
        order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        order.AddAssignedEmployee(OrderEmployee.Create(order, assignee));
        return order;
    }
}
