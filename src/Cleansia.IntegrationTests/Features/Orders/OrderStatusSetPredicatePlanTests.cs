using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The two production call sites that test an order's status with <c>Contains</c> close over
/// different shapes — <c>OrderSpecification</c> over an instance <c>IEnumerable&lt;OrderStatus&gt;</c>,
/// <c>OrderRepository.LiveCommitmentsInWindow</c> over a <c>private static readonly OrderStatus[]</c> —
/// and both carry a comment claiming the term seeks on the leading column of
/// <c>IX_Orders_CurrentStatus_CleaningDateTime</c>. They do emit different SQL (a parameterised
/// <c>= ANY(@p)</c> vs an inlined <c>IN (…)</c>), which PostgreSQL folds to the same
/// <c>ScalarArrayOpExpr</c>; the claim is therefore true today and nothing kept it true.
///
/// <para>These tests execute each call site through its real production entry point against real
/// PostgreSQL, capture the statement EF actually emits, and EXPLAIN that captured statement on the
/// same connection with the same parameters — so the plan asserted is the plan the query gets, not
/// the plan of a hand-written mirror of it.</para>
/// </summary>
public sealed class OrderStatusSetPredicatePlanTests(OrderStatusSetPredicatePlanTests.Fixture fixture)
    : IClassFixture<OrderStatusSetPredicatePlanTests.Fixture>
{
    private const string StatusIndex = "IX_Orders_CurrentStatus_CleaningDateTime";

    private const string SpecCompletedDashboard = "OrderSpecification → CreateCompletedOrdersSpec";
    private const string RepoOverlapWriteGate = "LiveCommitmentsInWindow → HasOverlappingOrderAsync";
    private const string RepoBusySetBoardFloor = "LiveCommitmentsInWindow → GetBusyEmployeeIdsInWindowAsync";

    private static readonly string[] PinnedQueries =
        [SpecCompletedDashboard, RepoOverlapWriteGate, RepoBusySetBoardFloor];

    // A golden copy, deliberately NOT read off the private SlotBlockingStatuses field: an expectation
    // that moves with the array under test cannot detect the array being widened.
    private static readonly int[] ExpectedLiveCommitmentStatuses =
    [
        (int)OrderStatus.New,
        (int)OrderStatus.Pending,
        (int)OrderStatus.Confirmed,
        (int)OrderStatus.OnTheWay,
        (int)OrderStatus.InProgress,
    ];

    [Fact]
    public void EveryPinnedQueryWasCaptured()
    {
        Assert.Equal(PinnedQueries.Length, fixture.Captured.Count);

        foreach (var key in PinnedQueries)
        {
            Assert.True(fixture.Captured.ContainsKey(key), $"{key}: no statement was captured.");
            Assert.False(string.IsNullOrWhiteSpace(fixture.Captured[key].Plan), $"{key}: empty plan.");
        }
    }

    [Fact]
    public void TheSeedIsSkewedEnoughForThePlannerToHaveAChoice()
    {
        Assert.True(fixture.OrderCount >= 30000, $"Orders rows: {fixture.OrderCount}.");
        Assert.True(
            fixture.LiveCommitmentShare < 0.15,
            $"live-status share of Orders is {fixture.LiveCommitmentShare:P1} — a status band that " +
            "selects most of the table makes a seq scan the correct plan and the assertion vacuous.");
    }

    // ── AC5 / anti-vacuity: the pinned queries match real rows, so the plans are not plans of nothing.

    [Fact]
    public void ThePinnedQueriesReturnNonDegenerateResults()
    {
        Assert.True(fixture.CompletedDashboardCount > 0, "completed-dashboard count was 0.");
        Assert.True(fixture.HasOverlap, "the overlap write gate found no conflicting order.");
        Assert.Contains(Fixture.ProbeEmployeeId, fixture.BusyEmployeeIds);
    }

    // ── AC2: the status term is an INDEX CONDITION on the intended index, in every call site.

    [Fact]
    public void EveryStatusSetPredicateIsAnIndexConditionOnTheCurrentStatusIndex()
    {
        Assert.Equal(PinnedQueries.Length, fixture.Captured.Count);

        foreach (var key in PinnedQueries)
        {
            var cond = IndexConditionFor(fixture.Captured[key].Plan);

            Assert.True(
                cond is not null,
                $"{key}: no `Index Cond:` on {StatusIndex}. The status term stopped being an index " +
                $"qual.\n--- SQL ---\n{fixture.Captured[key].Sql}\n--- PLAN ---\n{fixture.Captured[key].Plan}");

            Assert.True(
                StatusScalarArrayOp().IsMatch(cond!),
                $"{key}: {StatusIndex} is used, but the status term is not the leading index " +
                $"condition — it has been demoted to a residual filter.\n--- INDEX COND ---\n{cond}");
        }
    }

    [Fact]
    public void NoPinnedQueryFallsBackToASeqScanOnOrders()
    {
        Assert.Equal(PinnedQueries.Length, fixture.Captured.Count);

        foreach (var key in PinnedQueries)
        {
            Assert.False(
                fixture.Captured[key].Plan.Contains("Seq Scan on \"Orders\"", StringComparison.Ordinal),
                $"{key}: the planner fell back to a seq scan on Orders.\n" +
                $"--- PLAN ---\n{fixture.Captured[key].Plan}");
        }
    }

    // ── AC4: the two shapes emit different SQL and PostgreSQL folds both to the same operator.

    [Fact]
    public void TheInstancePropertyShapeEmitsAParameterisedAnyAndTheStaticArrayShapeEmitsAnInlinedInList()
    {
        Assert.Matches(
            new Regex("""o\."CurrentStatus" = ANY \(@\w+\)"""),
            fixture.Captured[SpecCompletedDashboard].Sql);

        var inlined = $"o.\"CurrentStatus\" IN ({string.Join(", ", ExpectedLiveCommitmentStatuses)})";
        Assert.Contains(inlined, fixture.Captured[RepoOverlapWriteGate].Sql, StringComparison.Ordinal);
        Assert.Contains(inlined, fixture.Captured[RepoBusySetBoardFloor].Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BothContainsShapesNormaliseToTheSameScalarArrayOperator()
    {
        Assert.Equal(PinnedQueries.Length, fixture.Captured.Count);

        var operators = PinnedQueries
            .Select(key => (Key: key, Op: StatusScalarArrayOp().Match(IndexConditionFor(fixture.Captured[key].Plan) ?? "")))
            .ToList();

        Assert.All(operators, entry => Assert.True(
            entry.Op.Success,
            $"{entry.Key}: the status term did not normalise to `\"CurrentStatus\" = ANY ('{{…}}'::integer[])`.\n" +
            $"--- PLAN ---\n{fixture.Captured[entry.Key].Plan}"));

        var shapes = operators
            .Select(entry => entry.Op.Value.Replace(entry.Op.Groups["set"].Value, "{…}", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            shapes.Count == 1,
            "the two Contains shapes no longer fold to one operator — the SQL difference has become a " +
            $"plan difference: {string.Join(" | ", shapes)}");
    }

    // ── AC3: the live-commitment set that actually reaches PostgreSQL, value for value.

    [Fact]
    public void TheLiveCommitmentStatusSetReachesPostgresExactly()
    {
        foreach (var key in new[] { RepoOverlapWriteGate, RepoBusySetBoardFloor })
        {
            var match = StatusScalarArrayOp().Match(IndexConditionFor(fixture.Captured[key].Plan) ?? "");
            Assert.True(match.Success, $"{key}: no status operator in the index condition.");

            var emitted = match.Groups["set"].Value
                .Trim('{', '}')
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => int.Parse(v, CultureInfo.InvariantCulture))
                .ToArray();

            Assert.True(emitted.Length > 0, $"{key}: the emitted status set was empty.");
            Assert.True(
                ExpectedLiveCommitmentStatuses.SequenceEqual(emitted),
                $"{key}: the live-commitment status set changed. " +
                $"expected [{string.Join(",", ExpectedLiveCommitmentStatuses)}] " +
                $"but PostgreSQL received [{string.Join(",", emitted)}].");
        }
    }

    /// <summary>The <c>Index Cond:</c> line belonging to the scan node that names the status index.</summary>
    private static string? IndexConditionFor(string plan)
    {
        var lines = plan.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(StatusIndex, StringComparison.Ordinal))
            {
                continue;
            }

            for (var j = i + 1; j < lines.Length && !lines[j].Contains("Scan", StringComparison.Ordinal); j++)
            {
                if (lines[j].Contains("Index Cond:", StringComparison.Ordinal))
                {
                    return lines[j].Trim();
                }
            }
        }

        return null;
    }

    private static Regex StatusScalarArrayOp() =>
        new(@"""CurrentStatus"" = ANY \('(?<set>\{[^']*\})'::integer\[\]\)");

    public sealed class Fixture : IAsyncLifetime
    {
        public const string ProbeEmployeeId = "emp-plan-probe";

        private static readonly DateTime WindowStart = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:latest")
            .WithDatabase("orderplandb")
            .WithUsername("planuser")
            .WithPassword("planpass")
            .Build();

        private readonly PlanCapturingInterceptor _interceptor = new();

        public Dictionary<string, CapturedStatement> Captured { get; } = new(StringComparer.Ordinal);

        public int OrderCount { get; private set; }

        public double LiveCommitmentShare { get; private set; }

        public int CompletedDashboardCount { get; private set; }

        public bool HasOverlap { get; private set; }

        public IReadOnlySet<string> BusyEmployeeIds { get; private set; } = new HashSet<string>();

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            await using (var ctx = NewContext())
            {
                await ctx.Database.EnsureCreatedAsync();
            }

            await using var conn = new NpgsqlConnection(_container.GetConnectionString());
            await conn.OpenAsync();
            await Execute(conn, DropOrderForeignKeys);
            await SeedSkewedDatasetAsync(conn);
            await Execute(conn, "ANALYZE \"Orders\"; ANALYZE \"OrderEmployees\";");

            OrderCount = await ScalarAsync(conn, "SELECT count(*)::int FROM \"Orders\";");
            var live = await ScalarAsync(conn,
                "SELECT count(*)::int FROM \"Orders\" WHERE \"CurrentStatus\" IN (0,1,2,3,4);");
            LiveCommitmentShare = (double)live / OrderCount;

            await CaptureAsync(SpecCompletedDashboard, async ctx =>
            {
                var spec = DashboardSpecifications.CreateCompletedOrdersSpec(
                    ProbeEmployeeId, WindowStart.AddDays(-30), WindowStart.AddDays(1));
                CompletedDashboardCount =
                    await new OrderRepository(ctx).GetCountAsync(spec.SatisfiedBy(), CancellationToken.None);
            });

            await CaptureAsync(RepoOverlapWriteGate, async ctx =>
                HasOverlap = await new OrderRepository(ctx).HasOverlappingOrderAsync(
                    ProbeEmployeeId, WindowStart, 120, CancellationToken.None));

            await CaptureAsync(RepoBusySetBoardFloor, async ctx =>
                BusyEmployeeIds = await new OrderRepository(ctx).GetBusyEmployeeIdsInWindowAsync(
                    [ProbeEmployeeId, "emp-plan-other"], WindowStart, WindowStart.AddHours(2),
                    CancellationToken.None));
        }

        public async Task DisposeAsync()
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }

        private async Task CaptureAsync(string key, Func<CleansiaDbContext, Task> act)
        {
            _interceptor.Reset();
            await using var ctx = NewContext();
            await act(ctx);
            Captured[key] = Assert.Single(_interceptor.Statements);
        }

        private CleansiaDbContext NewContext() =>
            new(
                new DbContextOptionsBuilder<CleansiaDbContext>()
                    .UseNpgsql($"{_container.GetConnectionString()};Pooling=false")
                    .AddInterceptors(_interceptor)
                    .Options,
                new TestUserSessionProvider("system", "system@cleansia.test"),
                new NullTenantProvider());

        private const string DropOrderForeignKeys = """
            DO $$
            DECLARE r record;
            BEGIN
              FOR r IN SELECT conname, conrelid::regclass AS tbl FROM pg_constraint
                       WHERE contype = 'f'
                         AND conrelid IN ('"Orders"'::regclass, '"OrderEmployees"'::regclass)
              LOOP
                EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', r.tbl, r.conname);
              END LOOP;
            END $$;
            """;

        /// <summary>
        /// Two years of orders dominated by terminal ones, the way a live table ages, with only a
        /// handful of live commitments inside the probe window. The probe cleaner also carries a long
        /// assignment history: without it the semi-join alone is selective enough that the planner
        /// drives the overlap query off the employee index, and the scan-floor rationale
        /// <c>LiveCommitmentsInWindow</c> documents is never exercised.
        /// </summary>
        private static async Task SeedSkewedDatasetAsync(NpgsqlConnection conn)
        {
            await Execute(conn, InsertOrders(
                idPrefix: "hist", count: 30000,
                statusSql: "CASE WHEN g % 5 = 0 THEN 6 ELSE 5 END",
                cleaningSql: $"'{Iso(WindowStart)}'::timestamptz - (g % 730) * interval '1 day'"));

            await Execute(conn, InsertOrders(
                idPrefix: "live", count: 2000,
                statusSql: "(g % 5)",
                cleaningSql: $"'{Iso(WindowStart)}'::timestamptz - (30 + (g % 700)) * interval '1 day'"));

            await Execute(conn, InsertOrders(
                idPrefix: "win", count: 8,
                statusSql: "(g % 5)",
                cleaningSql: $"'{Iso(WindowStart)}'::timestamptz + (g % 2) * interval '15 minutes'"));

            await Execute(conn, AssignEmployee("oe", ProbeEmployeeId, "\"Id\" LIKE 'win-%'"));
            await Execute(conn, AssignEmployee("oeh", ProbeEmployeeId,
                "\"Id\" LIKE 'hist-%' AND right(\"Id\", 1) IN ('0','1','2')"));
            await Execute(conn, AssignEmployee("oel", "emp-plan-other", "\"Id\" LIKE 'live-%'"));
        }

        private static string AssignEmployee(string idPrefix, string employeeId, string where) =>
            "INSERT INTO \"OrderEmployees\" (\"Id\",\"OrderId\",\"EmployeeId\",\"IsActive\") " +
            $"SELECT '{idPrefix}-' || \"Id\", \"Id\", '{employeeId}', true FROM \"Orders\" WHERE {where};";

        private static string InsertOrders(string idPrefix, int count, string statusSql, string cleaningSql) =>
            "INSERT INTO \"Orders\" (" +
            "\"Id\",\"CustomerName\",\"CustomerEmail\",\"CustomerPhone\",\"CustomerAddressId\"," +
            "\"DisplayOrderNumber\",\"Rooms\",\"Bathrooms\",\"CleaningDateTime\",\"PaymentType\"," +
            "\"PaymentStatus\",\"TotalPrice\",\"NetAmount\",\"VatAmount\",\"EstimatedTime\"," +
            "\"EmployeePayCalculated\",\"RequiredEmployees\",\"MaxEmployees\",\"ConfirmationCode\"," +
            "\"StripeSessionId\",\"CurrencyId\",\"Extras\",\"CurrentStatus\",\"IsActive\"," +
            "\"CreatedBy\",\"CreatedOn\") SELECT " +
            $"'{idPrefix}-' || g, 'Cust ' || g, 'c' || g || '@x.test', '+420' || g, 'addr-1', " +
            "'ORD-' || g, 2, 1, " + cleaningSql + ", 2, " +
            "2, 1200, 1000, 200, 120, " +
            "false, 1, 1, 'CONF' || g, " +
            "'sess-' || g, 'czk', '{}', " + statusSql + ", true, " +
            $"'seed', '{Iso(WindowStart)}'::timestamptz FROM generate_series(1, {count}) AS g;";

        private static async Task Execute(NpgsqlConnection conn, string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<int> ScalarAsync(NpgsqlConnection conn, string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        private static string Iso(DateTime value) =>
            value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffffZ", CultureInfo.InvariantCulture);

        private sealed class NullTenantProvider : ITenantProvider
        {
            public string? GetCurrentTenantId() => null;
            public void SetTenantOverride(string tenantId) { }
            public void ClearTenantOverride() { }
        }
    }

    public sealed record CapturedStatement(string Sql, string Plan);

    /// <summary>
    /// EXPLAINs the statement EF is about to run, on the same connection, transaction and parameter
    /// values. A hand-written mirror of the SQL would pin the plan of the mirror.
    /// </summary>
    private sealed class PlanCapturingInterceptor : DbCommandInterceptor
    {
        public List<CapturedStatement> Statements { get; } = [];

        public void Reset() => Statements.Clear();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await RecordAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            await RecordAsync(command, cancellationToken);
            return result;
        }

        private async Task RecordAsync(DbCommand command, CancellationToken ct)
        {
            if (!command.CommandText.Contains("\"Orders\"", StringComparison.Ordinal))
            {
                return;
            }

            await using var explain = command.Connection!.CreateCommand();
            explain.CommandText = "EXPLAIN " + command.CommandText;
            explain.Transaction = command.Transaction;
            foreach (DbParameter parameter in command.Parameters)
            {
                explain.Parameters.Add(((ICloneable)parameter).Clone());
            }

            var lines = new List<string>();
            await using (var reader = await explain.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    lines.Add(reader.GetString(0));
                }
            }

            Statements.Add(new CapturedStatement(command.CommandText, string.Join("\n", lines)));
        }
    }
}
