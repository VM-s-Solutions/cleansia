using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Specifications;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// <see cref="OrderSpecification"/>'s status filter moved from a per-row latest-history correlated
/// subquery onto the persisted <c>Orders.CurrentStatus</c> column. These tests pin that the migrated
/// filter selects EXACTLY the rows the history-derived rule (CreatedOn desc, then Sequence desc)
/// selects, over a seeded population that includes same-timestamp Sequence ties,
/// Cancelled-after-Confirmed, and a history-less order — the expected set is independently
/// recomputed from the OrderStatusHistory rows, never from the column.
/// </summary>
public sealed class OrderSpecificationCurrentStatusTests : IAsyncLifetime, IDisposable
{
    private const string BoardCaller = "spec-board-caller";
    private const string BoardOther = "spec-board-other";
    private static readonly DateTime BoardNow = new(2035, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private readonly SqliteConnection _connection;

    public OrderSpecificationCurrentStatusTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext(Action<string>? log = null)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection);
        if (log is not null)
        {
            options.LogTo(log, [RelationalEventId.CommandExecuted]);
        }

        return new(
            options.Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));
    }

    public async Task InitializeAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var baseStamp = DateTimeOffset.UtcNow.AddDays(-1);
        Seed(ctx, "spec-pending", baseStamp, OrderStatus.New, OrderStatus.Pending);
        Seed(ctx, "spec-confirmed", baseStamp, OrderStatus.New, OrderStatus.Pending, OrderStatus.Confirmed);
        Seed(ctx, "spec-cancelled-after-confirmed", baseStamp, OrderStatus.New, OrderStatus.Confirmed, OrderStatus.Cancelled);
        Seed(ctx, "spec-completed", baseStamp, OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress, OrderStatus.Completed);

        // Same-timestamp tie: Confirmed and Cancelled share one CreatedOn; Sequence decides → Cancelled.
        var tied = NewOrder("spec-tie");
        var tick = baseStamp.AddHours(2);
        AppendTrack(tied, OrderStatus.Confirmed, tick);
        AppendTrack(tied, OrderStatus.Cancelled, tick);
        ctx.Add(tied);

        // No history at all: excluded from every status filter under both the old and new rule.
        ctx.Add(NewOrder("spec-no-history"));

        await ctx.CommitAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.InProgress)]
    public async Task Status_Filter_Matches_History_Derived_Rule(params OrderStatus[] statuses)
    {
        var expected = await ExpectedIdsFromHistoryAsync(statuses);

        await using var ctx = NewContext();
        var repository = new OrderRepository(ctx);
        var specification = OrderSpecification.Create(orderStatuses: statuses);

        var actual = await repository
            .GetFiltered(specification.SatisfiedBy())
            .Select(o => o.Id)
            .ToListAsync(CancellationToken.None);
        var count = await repository.GetCountAsync(specification.SatisfiedBy(), CancellationToken.None);

        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
        Assert.Equal(expected.Count, count);
    }

    [Fact]
    public async Task Tied_Timestamps_Resolve_By_Sequence_In_The_Filter()
    {
        await using var ctx = NewContext();
        var repository = new OrderRepository(ctx);
        var cancelledSpec = OrderSpecification.Create(orderStatuses: new[] { OrderStatus.Cancelled });
        var confirmedSpec = OrderSpecification.Create(orderStatuses: new[] { OrderStatus.Confirmed });

        var cancelledIds = await repository
            .GetFiltered(cancelledSpec.SatisfiedBy())
            .Select(o => o.Id)
            .ToListAsync(CancellationToken.None);
        var confirmedIds = await repository
            .GetFiltered(confirmedSpec.SatisfiedBy())
            .Select(o => o.Id)
            .ToListAsync(CancellationToken.None);

        Assert.Contains("spec-tie", cancelledIds);
        Assert.DoesNotContain("spec-tie", confirmedIds);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    public async Task Board_Seat_And_Visibility_Filters_Keep_Exact_Count_And_Ordered_Ids(
        bool? hasAvailableSpots, bool cashJobsHidden)
    {
        await SeedBoardRows();
        var specification = BoardSpecification(hasAvailableSpots);
        specification.HideCashFromEmployeeId = cashJobsHidden ? BoardCaller : null;
        var expected = ExpectedBoardIds(hasAvailableSpots, cashJobsHidden);

        await using var ctx = NewContext();
        var repository = new OrderRepository(ctx);
        var filter = specification.SatisfiedBy();
        var count = await repository.GetCountAsync(filter, CancellationToken.None);
        var ids = await repository.GetFiltered(filter)
            .OrderByDescending(o => o.Id)
            .Select(o => o.Id)
            .ToListAsync(CancellationToken.None);

        Assert.Equal(expected.Length, count);
        Assert.Equal(expected.OrderByDescending(id => id, StringComparer.Ordinal), ids);
    }

    [Theory]
    [InlineData(true, true, 1)]
    [InlineData(false, true, 1)]
    [InlineData(null, true, 1)]
    [InlineData(true, false, 1)]
    [InlineData(false, false, 0)]
    [InlineData(null, false, 0)]
    public async Task Board_Count_And_Ordered_Page_Execute_One_Takeable_Seat_Aggregate(
        bool? hasAvailableSpots, bool restrictToCaller, int expectedSeatAggregates)
    {
        await SeedBoardRows();
        var commands = new List<string>();
        await using var ctx = NewContext(commands.Add);
        var repository = new OrderRepository(ctx);
        var specification = BoardSpecification(hasAvailableSpots);
        specification.Id = "seat-empty";
        specification.RestrictToEmployeeId = restrictToCaller ? BoardCaller : null;
        var filter = specification.SatisfiedBy();

        var count = await repository.GetCountAsync(filter, CancellationToken.None);
        var ids = await repository.GetFiltered(filter)
            .OrderByDescending(o => o.CreatedOn)
            .ThenByDescending(o => o.Id)
            .Take(20)
            .Select(o => o.Id)
            .ToListAsync(CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Equal(new[] { "seat-empty" }, ids);
        Assert.Equal(2, commands.Count);
        Assert.Contains(commands, sql => sql.Contains("COUNT(*)", StringComparison.Ordinal));
        Assert.Contains(commands, sql => sql.Contains("ORDER BY", StringComparison.Ordinal)
            && sql.Contains("LIMIT", StringComparison.Ordinal));
        Assert.All(commands, sql => Assert.Equal(
            expectedSeatAggregates,
            Regex.Matches(sql, "\"CoverRequestedAt\" IS NULL").Count));
    }

    [Theory]
    [InlineData("seat-inactive", "active", false)]
    [InlineData("seat-empty", "active", true)]
    [InlineData("seat-own-cancel-cover", "offerable", false)]
    [InlineData("seat-own-cover", "offerable", true)]
    [InlineData("seat-own-cancel-cover", "new-status", false)]
    [InlineData("seat-own-cover", "new-status", true)]
    [InlineData("seat-foreign-open", "own-filter", false)]
    [InlineData("seat-own-open", "own-filter", true)]
    [InlineData("seat-own-open", "exclude", false)]
    [InlineData("seat-foreign-open", "exclude", true)]
    [InlineData("seat-foreign-cover", "unassigned", false)]
    [InlineData("seat-empty", "unassigned", true)]
    [InlineData("seat-own-eur", "currency-czk", false)]
    [InlineData("seat-own-open", "currency-czk", true)]
    public async Task Available_Board_Preserves_Independent_Request_Filters(
        string orderId, string requestFilter, bool expected)
    {
        await SeedBoardRows();
        var specification = BoardSpecification(true);
        specification.Id = orderId;
        switch (requestFilter)
        {
            case "active": specification.IsActive = true; break;
            case "offerable": specification.OfferableOnly = true; break;
            case "new-status": specification.OrderStatuses = [OrderStatus.New]; break;
            case "own-filter": specification.EmployeeId = BoardCaller; break;
            case "exclude": specification.ExcludeEmployeeId = BoardCaller; break;
            case "unassigned": specification.IsUnassigned = true; break;
            case "currency-czk": specification.CurrencyId = "czk"; break;
            default: throw new ArgumentOutOfRangeException(nameof(requestFilter));
        }

        await using var ctx = NewContext();
        var repository = new OrderRepository(ctx);
        var filter = specification.SatisfiedBy();
        var ids = await repository.GetFiltered(filter).Select(o => o.Id).ToListAsync();
        var count = await repository.GetCountAsync(filter, CancellationToken.None);

        Assert.Equal(expected ? new[] { orderId } : Array.Empty<string>(), ids);
        Assert.Equal(expected ? 1 : 0, count);
    }

    private static OrderSpecification BoardSpecification(bool? hasAvailableSpots) => OrderSpecification.Create(
        customerName: "Board regression",
        hasAvailableSpots: hasAvailableSpots,
        restrictToEmployeeId: BoardCaller,
        nowUtc: BoardNow,
        notHeldFromEmployeeId: BoardCaller,
        cleanerCurrencyId: "czk");

    private static string[] ExpectedBoardIds(bool? hasAvailableSpots, bool cashJobsHidden)
    {
        var ids = new List<string>
        {
            "seat-empty", "seat-own-open", "seat-foreign-open", "seat-own-cover", "seat-foreign-cover",
            "seat-multi-cover", "seat-own-cancel-open", "seat-own-cancel-cover", "seat-own-unpaid",
            "seat-own-cash", "seat-own-eur", "seat-my-hold", "seat-expired-hold", "seat-hold-boundary",
            "seat-held-assigned", "seat-inactive", "seat-started",
        };
        if (!cashJobsHidden)
        {
            ids.AddRange(["seat-cash", "seat-cash-confirmed"]);
        }
        if (hasAvailableSpots != true)
        {
            ids.AddRange(["seat-own-full", "seat-own-cancel-full", "seat-own-zero"]);
        }
        return ids.ToArray();
    }

    private async Task SeedBoardRows()
    {
        await using var ctx = NewContext();
        var callerUser = User.CreateWithPassword("spec-board-caller@cleansia.test", "Test-password-1!", "Board", "Caller", UserProfile.Employee);
        var caller = Employee.CreateWithUser(callerUser);
        caller.Id = BoardCaller;
        var otherUser = User.CreateWithPassword("spec-board-other@cleansia.test", "Test-password-1!", "Board", "Other", UserProfile.Employee);
        var other = Employee.CreateWithUser(otherUser);
        other.Id = BoardOther;
        ctx.AddRange(caller, other);

        Order Add(string id, Employee? assignee = null, int max = 1, bool cover = false,
            OrderStatus status = OrderStatus.New, PaymentType type = PaymentType.Card,
            PaymentStatus payment = PaymentStatus.Paid, string? recurring = null,
            string currency = "czk", string? heldFor = null, DateTime? holdUntil = null)
        {
            var order = Order.Create("Board regression", "board@cleansia.test", "+420000000000",
                Address.Create("Board St", "Prague", "11000", "cz"), 1, 1, BoardNow.AddDays(1),
                type, 1000m, currency, payment, BookingPolicy.CancellationTermsAtBooking,
                recurringTemplateId: recurring);
            order.Id = id;
            order.Created("system", BoardNow.AddDays(-2));
            order.SetMaxEmployees(max);
            AppendTrack(order, status, BoardNow.AddDays(-1));
            if (heldFor is not null)
            {
                order.GrantPreferredHold(heldFor, holdUntil ?? BoardNow.AddHours(1), BoardNow.AddHours(-2), BookingPolicy.MaxPreferredOfferRounds);
            }
            if (assignee is not null)
            {
                var assignment = OrderEmployee.Create(order, assignee);
                order.AddAssignedEmployee(assignment);
                if (cover)
                {
                    assignment.MarkCoverRequested(BoardNow.AddHours(-1));
                    Assert.False(order.HasAvailableSpots);
                    Assert.True(order.HasTakeableSeat);
                }
            }
            ctx.Add(order);
            return order;
        }

        Add("seat-empty");
        Add("seat-own-open", caller, max: 2);
        Add("seat-foreign-open", other, max: 2);
        Add("seat-own-full", caller);
        Add("seat-foreign-full", other);
        Add("seat-own-cover", caller, cover: true);
        Add("seat-foreign-cover", other, cover: true);
        var multi = Add("seat-multi-cover", caller, max: 2);
        var second = OrderEmployee.Create(multi, other);
        multi.AddAssignedEmployee(second);
        second.MarkCoverRequested(BoardNow.AddHours(-1));
        Assert.False(multi.HasAvailableSpots);
        Assert.True(multi.HasTakeableSeat);
        Add("seat-own-cancel-open", caller, max: 2, status: OrderStatus.Cancelled);
        Add("seat-own-cancel-cover", caller, cover: true, status: OrderStatus.Cancelled);
        Add("seat-own-cancel-full", caller, status: OrderStatus.Cancelled);
        Add("seat-foreign-cancel", other, max: 2, status: OrderStatus.Cancelled);
        Add("seat-pending-status", status: OrderStatus.Pending);
        Add("seat-unpaid", payment: PaymentStatus.Pending);
        Add("seat-own-unpaid", caller, max: 2, payment: PaymentStatus.Pending);
        Add("seat-cash", type: PaymentType.Cash, payment: PaymentStatus.Pending);
        Add("seat-cash-unconfirmed", type: PaymentType.Cash, payment: PaymentStatus.Pending, recurring: "board-recurring-unconf");
        Add("seat-cash-confirmed", type: PaymentType.Cash, payment: PaymentStatus.Pending, recurring: "board-recurring-conf")
            .ConfirmByCustomer(BoardNow.AddHours(-1));
        Add("seat-own-cash", caller, max: 2, type: PaymentType.Cash, payment: PaymentStatus.Pending, recurring: "board-recurring-own");
        Add("seat-eur", currency: "eur");
        Add("seat-own-eur", caller, max: 2, currency: "eur");
        Add("seat-my-hold", heldFor: BoardCaller);
        Add("seat-other-hold", heldFor: BoardOther);
        Add("seat-expired-hold", heldFor: BoardOther, holdUntil: BoardNow.AddHours(-1));
        Add("seat-hold-boundary", heldFor: BoardOther, holdUntil: BoardNow);
        Add("seat-held-assigned", caller, max: 2, heldFor: BoardOther);
        Add("seat-inactive").IsActive = false;
        Add("seat-started", status: OrderStatus.InProgress);
        Add("seat-foreign-tenant");
        Add("seat-zero");
        Add("seat-own-zero", caller);

        await ctx.CommitAsync(CancellationToken.None);
        // Persist legacy edge rows without changing the domain's valid capacity rules.
        await ctx.Database.ExecuteSqlRawAsync(
            "UPDATE \"Orders\" SET \"MaxEmployees\" = 0 WHERE \"Id\" IN ('seat-zero', 'seat-own-zero')");
        await ctx.Database.ExecuteSqlRawAsync(
            "UPDATE \"Orders\" SET \"TenantId\" = 'foreign-board-tenant' WHERE \"Id\" = 'seat-foreign-tenant'");
    }

    private async Task<List<string>> ExpectedIdsFromHistoryAsync(OrderStatus[] statuses)
    {
        await using var ctx = NewContext();
        var tracks = await ctx.Set<OrderStatusTrack>().ToListAsync(CancellationToken.None);
        return tracks
            .GroupBy(t => t.OrderId)
            .Select(g => new
            {
                OrderId = g.Key,
                Latest = g.OrderByDescending(t => t.CreatedOn).ThenByDescending(t => t.Sequence).First().Status,
            })
            .Where(x => statuses.Contains(x.Latest))
            .Select(x => x.OrderId)
            .ToList();
    }

    private static void Seed(CleansiaDbContext ctx, string orderId, DateTimeOffset baseStamp, params OrderStatus[] statuses)
    {
        var order = NewOrder(orderId);
        var stamp = baseStamp;
        foreach (var status in statuses)
        {
            AppendTrack(order, status, stamp);
            stamp = stamp.AddMinutes(20);
        }
        ctx.Add(order);
    }

    private static Order NewOrder(string orderId)
    {
        var address = Address.Create("123 Main St", "Prague", "11000", "cz");
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = orderId;
        order.Created("system", DateTimeOffset.UtcNow.AddDays(-2));
        return order;
    }

    private static void AppendTrack(Order order, OrderStatus status, DateTimeOffset createdOn)
    {
        var track = OrderStatusTrack.Create(status, order);
        track.Created("system", createdOn);
        order.AddOrderStatus(track);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
