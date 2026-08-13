using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The race the unit suite cannot express. <c>TakeOrderSeatRaceTests</c> is a mocked test of the
/// SEQUENTIAL case — a second cleaner loading an order that already has the first assigned — and that
/// case was always handled. This is the CONCURRENT one: two callers that both load the order before
/// either commits, which is what a "new job" broadcast and a lapsing ADR-0036 preferred hold both
/// produce, because they wake many cleaners at the same instant.
///
/// Before the seat ordinal, both inserts succeeded and the order carried two cleaners against
/// <c>MaxEmployees = 1</c> — two <c>OrderEmployeePay</c> rows, a second full wage against an unchanged
/// customer price.
///
/// <para>This is the test the suite lacked. It runs against real Postgres because the guarantee is a
/// database one: the unique index is the only thing that separates the two callers, and no in-memory
/// harness can express that.</para>
/// </summary>
[Collection("PostgresCollection")]
public class TakeOrderConcurrentSeatRaceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    [Fact]
    public async Task Two_Concurrent_Takes_Of_One_Seat_Leave_Exactly_One_Assignment()
    {
        // Ids are varchar(26) — a bare ULID is exactly that width.
        var orderId = Ulid.NewUlid().ToString();
        var employeeA = Ulid.NewUlid().ToString();
        var employeeB = Ulid.NewUlid().ToString();

        await SeedSingleSeatOrderAsync(orderId, employeeA, employeeB);

        // Two independent contexts = two independent connections = a genuine race. Both read a free
        // seat, both derive ordinal 0, both attempt the insert.
        await using var contextA = NewContext();
        await using var contextB = NewContext();

        var orderA = await LoadWithAssignmentsAsync(contextA, orderId);
        var orderB = await LoadWithAssignmentsAsync(contextB, orderId);

        Assert.Empty(orderA.AssignedEmployees);
        Assert.Empty(orderB.AssignedEmployees);

        orderA.AddAssignedEmployee(OrderEmployee.Create(orderA, await LoadEmployeeAsync(contextA, employeeA)));
        orderB.AddAssignedEmployee(OrderEmployee.Create(orderB, await LoadEmployeeAsync(contextB, employeeB)));

        // CommitAsync, not SaveChangesAsync: CleansiaDbContext stamps audit + tenant fields in
        // CommitAsync and only then calls SaveChangesAsync, so this is also the production path.
        var saveA = contextA.CommitAsync(CancellationToken.None);
        var saveB = contextB.CommitAsync(CancellationToken.None);

        var outcomes = await Task.WhenAll(Capture(saveA), Capture(saveB));

        // Exactly one winner and one unique-violation — not two winners, and not two losers.
        Assert.Equal(1, outcomes.Count(o => o is null));
        var rejection = Assert.Single(outcomes, o => o is not null)!;
        Assert.IsType<DbUpdateException>(rejection);

        await using var verify = NewContext();
        var persisted = await LoadWithAssignmentsAsync(verify, orderId);
        Assert.Single(persisted.AssignedEmployees);
        Assert.Equal(0, persisted.AssignedEmployees.Single().SeatOrdinal);
    }

    private static async Task<Exception?> Capture(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    // Must carry a session + tenant provider: CleansiaDbContext stamps CreatedBy/TenantId from them at
    // SaveChanges, and the options-only constructor (used by the migration runner) stamps neither.
    private CleansiaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CleansiaDbContext>()
                .UseNpgsql(Fixture.GetConnectionString())
                .Options,
            new TestUserSessionProvider("seat-race", "seat-race@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

    private static Task<Order> LoadWithAssignmentsAsync(CleansiaDbContext context, string orderId) =>
        context.Orders
            .Include(o => o.AssignedEmployees)
            .FirstAsync(o => o.Id == orderId);

    private static Task<Employee> LoadEmployeeAsync(CleansiaDbContext context, string employeeId) =>
        context.Employees.FirstAsync(e => e.Id == employeeId);

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }

    private async Task SeedSingleSeatOrderAsync(string orderId, params string[] employeeIds)
    {
        await using var context = NewContext();

        // The Postgres container is shared across the collection, so reference rows another test may
        // already have seeded are added only when missing.
        if (!await context.Languages.AnyAsync(l => l.Code == "en"))
        {
            context.Languages.Add(Language.Create("en", "English"));
        }

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = Ulid.NewUlid().ToString();

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = Ulid.NewUlid().ToString();
        context.Countries.Add(country);
        context.Currencies.Add(currency);

        var address = Address.Create("123 Main St", "Prague", "11000", country.Id);
        var order = Order.Create(
            customerName: "Race Customer",
            customerEmail: $"{orderId}@example.com",
            customerPhone: "+420000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Pending);
        order.Id = orderId;
        order.SetMaxEmployees(1);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

        context.Orders.Add(order);

        foreach (var employeeId in employeeIds)
        {
            var user = User.CreateWithPassword($"{employeeId}@example.com", "12345678Test!", "Emp", "Loyee");
            user.Id = Ulid.NewUlid().ToString();
            var employee = Employee.CreateWithUser(user);
            employee.Id = employeeId;
            context.Employees.Add(employee);
        }

        await context.CommitAsync(CancellationToken.None);
    }
}
