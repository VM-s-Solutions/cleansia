using Cleansia.Core.AppServices.Features.Orders;
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
/// TC-PREF-EQUIV-0 (ADR-0036 D5.0), over REAL Postgres.
///
/// <para>The preferred-cleaner hold ships in two evaluation forms — a queryable one for the board and a
/// materialized one for the browse authorization. Sharing one expression tree would NOT make them
/// agree: <c>PreferredEmployeeId = @caller</c> with a null parameter is UNKNOWN under SQL's
/// three-valued logic and <c>true</c> under C# equality, so a caller with no employee id is "not the
/// beneficiary" in one form and "the beneficiary" in the other. That single row is the reason this test
/// runs against the real provider rather than an in-memory one.</para>
///
/// <para>The matrix is <c>deadline × beneficiary × assignment</c>, asked by three callers. Two of its
/// verdicts carry the safety properties: a live deadline with NO beneficiary must be open to everyone
/// (nothing may write that pair, but nothing may be stranded by it either), and a held order that
/// already has a cleaner must be open to everyone (first refusal is on the first seat, not a lease).
/// </para>
/// </summary>
[Collection("PostgresCollection")]
public class PreferredHoldVisibilityAgreementTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "cur-czk-hold";
    private const string CountryId = "ctry-cz-hold";
    private const string Beneficiary = "emp-hold-pref";
    private const string Bystander = "emp-hold-other";

    private static readonly DateTime Now = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Past = Now.AddHours(-1);
    private static readonly DateTime Future = Now.AddHours(3);

    private static readonly string?[] Deadlines = [null, "past", "future"];
    private static readonly string?[] Beneficiaries = [null, Beneficiary, Bystander];
    private static readonly string?[] Assignments = [null, Beneficiary, Bystander];

    [Theory]
    [InlineData(null)]
    [InlineData(Beneficiary)]
    [InlineData(Bystander)]
    public async Task The_Queryable_Form_And_The_In_Memory_Form_Select_The_Same_Rows(string? caller)
    {
        await TestMethod(
            arrange: SeedTheHoldMatrix,
            act: async provider =>
            {
                var repository = provider.GetRequiredService<IOrderRepository>();

                var fromSql = await repository
                    .GetQueryable()
                    .Where(OrderVisibility.NotHeldFrom(caller, Now))
                    .Select(o => o.Id)
                    .ToListAsync();

                var rows = await repository
                    .GetQueryable()
                    .Include(o => o.AssignedEmployees)
                    .AsSplitQuery()
                    .ToListAsync();

                var fromMemory = rows
                    .Where(o => OrderVisibility.NotHeldFrom(o, caller, Now))
                    .Select(o => o.Id)
                    .ToList();

                return (Sql: fromSql, Memory: fromMemory, RowCount: rows.Count);
            },
            assert: (CleansiaDbContext _, (List<string> Sql, List<string> Memory, int RowCount) result) =>
            {
                Assert.Equal(Cases().Count, result.RowCount);
                Assert.Equal(result.Sql.OrderBy(id => id), result.Memory.OrderBy(id => id));

                var expected = Cases()
                    .Where(c => IsOpen(c, caller))
                    .Select(c => c.OrderId)
                    .OrderBy(id => id);
                Assert.Equal(expected, result.Sql.OrderBy(id => id));

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The expected verdict, written out as the ADR's five terms rather than by calling the thing under
    /// test — otherwise the matrix would agree with any predicate, including a broken one.
    /// </summary>
    private static bool IsOpen(HoldCase scenario, string? caller) =>
        scenario.Deadline is null
        || scenario.Beneficiary is null
        || scenario.Deadline == "past"
        || (caller is not null && scenario.Beneficiary == caller)
        || scenario.Assignment is not null;

    /// <summary>Ids stay inside the ULID-width column, so the matrix is keyed by one letter per axis.</summary>
    private static string Tag(string? value) => value switch
    {
        null => "n",
        "past" => "p",
        "future" => "f",
        Beneficiary => "b",
        _ => "o",
    };

    private static List<HoldCase> Cases() =>
        [.. from deadline in Deadlines
            from beneficiary in Beneficiaries
            from assignment in Assignments
            select new HoldCase(
                $"h-{Tag(deadline)}{Tag(beneficiary)}{Tag(assignment)}",
                deadline,
                beneficiary,
                assignment)];

    private static async Task SeedTheHoldMatrix(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var cleaners = new Dictionary<string, Employee>
        {
            [Beneficiary] = NewCleaner(Beneficiary, "Anna", "Preferred"),
            [Bystander] = NewCleaner(Bystander, "Bea", "Bystander"),
        };
        foreach (var cleaner in cleaners.Values)
        {
            context.Add(cleaner);
        }

        var slot = 0;
        foreach (var scenario in Cases())
        {
            var order = NewOrder(scenario, Now.AddDays(2).AddHours(slot));
            if (scenario.Assignment is { } assignedTo)
            {
                order.AddAssignedEmployee(OrderEmployee.Create(order, cleaners[assignedTo]));
            }

            context.Add(order);
            slot++;
        }

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(HoldCase scenario, DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: "Hold Customer",
            customerEmail: "hold-customer@cleansia.test",
            customerPhone: "+420777111333",
            customerAddress: Address.Create("Hold St 1", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            preferredEmployeeId: scenario.Beneficiary);
        order.Id = scenario.OrderId;
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));

        if (scenario.Deadline is { } deadline)
        {
            var until = deadline == "past" ? Past : Future;
            if (scenario.Beneficiary is null)
            {
                // The stranded pair, reached the only way production could reach it: a write that
                // bypassed GrantPreferredHold, which refuses a deadline with no beneficiary.
                typeof(Order)
                    .GetProperty(nameof(Order.PreferredHoldUntilUtc))!
                    .GetSetMethod(nonPublic: true)!
                    .Invoke(order, [until]);
            }
            else
            {
                // Granted an hour before its own deadline, which is what production does — so an
                // already-expired fixture is a real past grant rather than a write the aggregate refuses.
                order.GrantPreferredHold(
                    scenario.Beneficiary, until, until.AddHours(-1), BookingPolicy.MaxPreferredOfferRounds);
            }
        }

        return order;
    }

    private static Employee NewCleaner(string employeeId, string firstName, string lastName)
    {
        var user = User.CreateWithPassword(
            $"{employeeId}@cleansia.test",
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            firstName,
            lastName,
            UserProfile.Employee);
        user.Id = $"user-{employeeId}";
        user.ConfirmEmail();
        user.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.Approve(approvedByUserId: "admin-hold");
        employee.AssignWorkCountry(CountryId);
        employee.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        return employee;
    }

    public sealed record HoldCase(string OrderId, string? Deadline, string? Beneficiary, string? Assignment);
}
