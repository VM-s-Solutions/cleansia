using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
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
/// <para><b>Why this is skipped.</b> The integration fixture applies the committed EF migration, and
/// <c>SeatOrdinal</c> + <c>IX_OrderEmployees_OrderId_SeatOrdinal</c> currently exist only in the model —
/// the migration is an owner-only step (pre-prod, folded into a regenerated <c>Initial</c>). Running now
/// would fail on a missing column and say nothing about the fix. Remove the Skip in the same change that
/// regenerates the migration; that is the whole checklist.</para>
/// </summary>
[Collection("PostgresCollection")]
public class TakeOrderConcurrentSeatRaceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string SkipUntilMigration =
        "MANUAL_STEP: needs the regenerated Initial migration carrying OrderEmployees.SeatOrdinal + "
        + "IX_OrderEmployees_OrderId_SeatOrdinal. Owner-only (see CLAUDE.md § Manual Steps).";

    [Fact(Skip = SkipUntilMigration)]
    public async Task Two_Concurrent_Takes_Of_One_Seat_Leave_Exactly_One_Assignment()
    {
        var orderId = $"seat-race-{Ulid.NewUlid()}";

        await SeedSingleSeatOrderAsync(orderId, "emp-race-a", "emp-race-b");

        // Two independent contexts = two independent connections = a genuine race. Both read a free
        // seat, both derive ordinal 0, both attempt the insert.
        await using var contextA = NewContext();
        await using var contextB = NewContext();

        var orderA = await LoadWithAssignmentsAsync(contextA, orderId);
        var orderB = await LoadWithAssignmentsAsync(contextB, orderId);

        Assert.Empty(orderA.AssignedEmployees);
        Assert.Empty(orderB.AssignedEmployees);

        orderA.AddAssignedEmployee(OrderEmployee.Create(orderA, await LoadEmployeeAsync(contextA, "emp-race-a")));
        orderB.AddAssignedEmployee(OrderEmployee.Create(orderB, await LoadEmployeeAsync(contextB, "emp-race-b")));

        var saveA = contextA.SaveChangesAsync();
        var saveB = contextB.SaveChangesAsync();

        var outcomes = await Task.WhenAll(Capture(saveA), Capture(saveB));

        // Exactly one winner and one unique-violation — not two winners, and not two losers.
        Assert.Equal(1, outcomes.Count(o => o is null));
        var rejection = Assert.Single(outcomes.Where(o => o is not null))!;
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

    private CleansiaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options);

    private static Task<Order> LoadWithAssignmentsAsync(CleansiaDbContext context, string orderId) =>
        context.Orders
            .Include(o => o.AssignedEmployees)
            .FirstAsync(o => o.Id == orderId);

    private static Task<Employee> LoadEmployeeAsync(CleansiaDbContext context, string employeeId) =>
        context.Employees.FirstAsync(e => e.Id == employeeId);

    private async Task SeedSingleSeatOrderAsync(string orderId, params string[] employeeIds)
    {
        await using var context = NewContext();

        var address = Address.Create("123 Main St", "Prague", "11000", "cz");
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
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending);
        order.Id = orderId;
        order.SetMaxEmployees(1);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

        context.Orders.Add(order);

        foreach (var employeeId in employeeIds)
        {
            var user = User.CreateWithPassword($"{employeeId}@example.com", "x", "Emp", "Loyee");
            user.Id = $"{employeeId}-user";
            var employee = Employee.CreateWithUser(user);
            employee.Id = employeeId;
            context.Employees.Add(employee);
        }

        await context.SaveChangesAsync();
    }
}
