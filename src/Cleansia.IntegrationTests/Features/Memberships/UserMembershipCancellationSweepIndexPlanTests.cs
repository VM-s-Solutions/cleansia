using System.Data.Common;
using System.Globalization;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Cleansia.IntegrationTests.Features.Memberships;

/// <summary>
/// Query-plan proof that both arms of the membership lifecycle sweep
/// (<see cref="SendMembershipLifecycleNotifications"/>) are served by their own partial index: the
/// renewal arm by <c>IX_UserMemberships_Status_CurrentPeriodEnd</c> (filtered
/// <c>RenewalReminderSentAt IS NULL</c>) and the cancellation arm by
/// <c>IX_UserMemberships_Status_CurrentPeriodEnd_Cancellation</c> (filtered
/// <c>CancelledAt IS NOT NULL AND CancellationReminderSentAt IS NULL</c>), whose predicates do not
/// cover each other.
///
/// <para>The assertion is that the <c>Status</c> equality and BOTH <c>CurrentPeriodEnd</c> bounds are
/// <b>index conditions</b>. "No seq scan" is kept but is not the pin: pushing the date band inside an
/// <c>OR</c> leaves the planner on the same index, demotes the band to a residual filter and costs 55×,
/// while a seq-scan assertion stays green. The seed is what makes the index-condition assertion mean
/// anything — each partial index is populated with thousands of rows OUTSIDE the date band, because
/// with only a handful inside the partial filter the index KEY is never exercised and an index keyed
/// on anything at all would pass.</para>
///
/// <para>The plan asserted is EXPLAIN of the statement EF actually emitted, captured off the real
/// handler on the same connection, transaction and parameter values — not of SQL retyped into the
/// test. Uses its own throwaway container with the schema built from the current EF model
/// (<c>EnsureCreated</c>); the FKs on UserMemberships are dropped so rows insert without a full
/// User/MembershipPlan graph.</para>
/// </summary>
public sealed class UserMembershipCancellationSweepIndexPlanTests(
    UserMembershipCancellationSweepIndexPlanTests.Fixture fixture)
    : IClassFixture<UserMembershipCancellationSweepIndexPlanTests.Fixture>
{
    private const string CancellationIndex = "IX_UserMemberships_Status_CurrentPeriodEnd_Cancellation";
    private const string RenewalIndex = "IX_UserMemberships_Status_CurrentPeriodEnd";

    private const string RenewalArm = "SendMembershipLifecycleNotifications → renewal reminders";
    private const string CancellationArm = "SendMembershipLifecycleNotifications → cancellation-effective reminders";

    private static readonly string[] PinnedQueries = [RenewalArm, CancellationArm];

    [Fact]
    public void BothSweepArmsWereCaptured()
    {
        Assert.Equal(PinnedQueries.Length, fixture.Captured.Count);

        foreach (var key in PinnedQueries)
        {
            Assert.True(fixture.Captured.ContainsKey(key), $"{key}: no statement was captured.");
            Assert.False(string.IsNullOrWhiteSpace(fixture.Captured[key].Plan), $"{key}: empty plan.");
        }
    }

    /// <summary>
    /// The anchor the previous version of this test lacked: its seed put five rows inside the
    /// cancellation partial filter, so the filter answered the query on its own and the index key was
    /// never exercised.
    /// </summary>
    [Fact]
    public void EachPartialIndexIsPopulatedEnoughForItsKeyToMatter()
    {
        Assert.True(
            fixture.RowsInCancellationFilter >= 5000,
            $"only {fixture.RowsInCancellationFilter} rows sit inside the cancellation partial filter — " +
            "the filter answers the query on its own and the (Status, CurrentPeriodEnd) key is untested.");
        Assert.True(
            fixture.RowsInRenewalFilter >= 5000,
            $"only {fixture.RowsInRenewalFilter} rows sit inside the renewal partial filter.");

        Assert.True(
            fixture.CancellationDueRows < fixture.RowsInCancellationFilter / 100.0,
            $"the cancellation sweep selects {fixture.CancellationDueRows} of " +
            $"{fixture.RowsInCancellationFilter} indexed rows — too large a share for the date band to " +
            "be the discriminating term.");
        Assert.True(
            fixture.RenewalDueRows < fixture.RowsInRenewalFilter / 100.0,
            $"the renewal sweep selects {fixture.RenewalDueRows} of {fixture.RowsInRenewalFilter} " +
            "indexed rows.");
    }

    [Fact]
    public void BothSweepArmsReturnNonDegenerateResults()
    {
        Assert.True(fixture.RenewalRemindersSent > 0, "the renewal arm matched no rows.");
        Assert.True(fixture.CancellationRemindersSent > 0, "the cancellation arm matched no rows.");
    }

    [Fact]
    public void EachArmSeeksOnItsOwnPartialIndex()
    {
        Assert.Equal(PinnedQueries.Length, fixture.Captured.Count);

        Assert.True(
            fixture.Captured[RenewalArm].Plan.Contains(RenewalIndex, StringComparison.Ordinal),
            $"{RenewalArm}: {RenewalIndex} is not in the plan.\n{fixture.Captured[RenewalArm].Plan}");
        Assert.False(
            fixture.Captured[RenewalArm].Plan.Contains(CancellationIndex, StringComparison.Ordinal),
            $"{RenewalArm}: the cancellation index leaked into the renewal plan.");

        Assert.True(
            fixture.Captured[CancellationArm].Plan.Contains(CancellationIndex, StringComparison.Ordinal),
            $"{CancellationArm}: {CancellationIndex} is not in the plan.\n" +
            fixture.Captured[CancellationArm].Plan);
    }

    /// <summary>
    /// The pin. A term inside an <c>OR</c> keeps the index and drops out of the index condition, which
    /// is exactly what a seq-scan assertion cannot see.
    /// </summary>
    [Fact]
    public void TheStatusEqualityAndBothDateBoundsAreIndexConditionsNotResidualFilters()
    {
        Assert.Equal(PinnedQueries.Length, fixture.Captured.Count);

        var expectedTerms = new[] { "\"Status\" = 1", "\"CurrentPeriodEnd\" >=", "\"CurrentPeriodEnd\" <=" };

        foreach (var (key, index) in new[] { (RenewalArm, RenewalIndex), (CancellationArm, CancellationIndex) })
        {
            var cond = IndexConditionFor(fixture.Captured[key].Plan, index);

            Assert.True(
                cond is not null,
                $"{key}: no `Index Cond:` on {index}.\n--- SQL ---\n{fixture.Captured[key].Sql}\n" +
                $"--- PLAN ---\n{fixture.Captured[key].Plan}");

            foreach (var term in expectedTerms)
            {
                Assert.True(
                    cond!.Contains(term, StringComparison.Ordinal),
                    $"{key}: `{term}` is not an index condition on {index} — it has been demoted to a " +
                    "residual filter, which keeps the plan on the index and off a seq scan while the " +
                    $"scan widens to the whole partial index.\n--- INDEX COND ---\n{cond}\n" +
                    $"--- PLAN ---\n{fixture.Captured[key].Plan}");
            }
        }
    }

    /// <summary>
    /// Kept, and deliberately weaker than the test above: it survives the regression that matters.
    /// </summary>
    [Fact]
    public void NeitherArmFallsBackToASeqScan()
    {
        Assert.Equal(PinnedQueries.Length, fixture.Captured.Count);

        foreach (var key in PinnedQueries)
        {
            Assert.False(
                fixture.Captured[key].Plan.Contains("Seq Scan on \"UserMemberships\"", StringComparison.Ordinal),
                $"{key}: the planner fell back to a seq scan.\n{fixture.Captured[key].Plan}");
        }
    }

    private static string? IndexConditionFor(string plan, string indexName)
    {
        var lines = plan.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(indexName, StringComparison.Ordinal))
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

    public sealed class Fixture : IAsyncLifetime
    {
        private static readonly DateTime Now = DateTime.UtcNow;

        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:latest")
            .WithDatabase("plandb")
            .WithUsername("planuser")
            .WithPassword("planpass")
            .Build();

        private readonly PlanCapturingInterceptor _interceptor = new();

        public Dictionary<string, CapturedStatement> Captured { get; } = new(StringComparer.Ordinal);

        public int RowsInCancellationFilter { get; private set; }

        public int RowsInRenewalFilter { get; private set; }

        public int CancellationDueRows { get; private set; }

        public int RenewalDueRows { get; private set; }

        public int RenewalRemindersSent { get; private set; }

        public int CancellationRemindersSent { get; private set; }

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            await using (var ctx = NewContext())
            {
                await ctx.Database.EnsureCreatedAsync();
            }

            await using var conn = new NpgsqlConnection(_container.GetConnectionString());
            await conn.OpenAsync();

            await Execute(conn,
                "ALTER TABLE \"UserMemberships\" DROP CONSTRAINT IF EXISTS \"FK_UserMemberships_Users_UserId\";" +
                "ALTER TABLE \"UserMemberships\" DROP CONSTRAINT IF EXISTS \"FK_UserMemberships_MembershipPlans_MembershipPlanId\";");

            await SeedSkewedDatasetAsync(conn);
            await Execute(conn, "ANALYZE \"UserMemberships\";");

            RowsInCancellationFilter = await Scalar(conn,
                "SELECT count(*)::int FROM \"UserMemberships\" " +
                "WHERE \"CancelledAt\" IS NOT NULL AND \"CancellationReminderSentAt\" IS NULL;");
            RowsInRenewalFilter = await Scalar(conn,
                "SELECT count(*)::int FROM \"UserMemberships\" WHERE \"RenewalReminderSentAt\" IS NULL;");
            CancellationDueRows = await Scalar(conn,
                "SELECT count(*)::int FROM \"UserMemberships\" " +
                "WHERE \"CancelledAt\" IS NOT NULL AND \"CancellationReminderSentAt\" IS NULL " +
                "AND \"Status\" = 1 AND \"CurrentPeriodEnd\" >= now() " +
                "AND \"CurrentPeriodEnd\" <= now() + interval '2 days';");
            RenewalDueRows = await Scalar(conn,
                "SELECT count(*)::int FROM \"UserMemberships\" " +
                "WHERE \"RenewalReminderSentAt\" IS NULL AND \"Status\" = 1 " +
                "AND \"CurrentPeriodEnd\" >= now() + interval '2 days' " +
                "AND \"CurrentPeriodEnd\" <= now() + interval '4 days';");

            await RunTheRealSweepAsync();
        }

        public async Task DisposeAsync()
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }

        /// <summary>
        /// The production handler, so the captured statements are the ones the sweep really issues.
        /// The renewal arm reads before the cancellation arm, which is what keys the capture.
        /// </summary>
        private async Task RunTheRealSweepAsync()
        {
            await using var ctx = NewContext();
            var repository = new UserMembershipRepository(ctx);

            var handler = new SendMembershipLifecycleNotifications.Handler(
                repository,
                new NoOpNotificationProducer(),
                repository,
                NullLogger<SendMembershipLifecycleNotifications.Handler>.Instance);

            var result = await handler.Handle(
                new SendMembershipLifecycleNotifications.Command(), CancellationToken.None);

            RenewalRemindersSent = result.Value!.RenewalRemindersSent;
            CancellationRemindersSent = result.Value.CancellationRemindersSent;

            Assert.Equal(2, _interceptor.Statements.Count);
            Captured[RenewalArm] = _interceptor.Statements[0];
            Captured[CancellationArm] = _interceptor.Statements[1];
        }

        private CleansiaDbContext NewContext() =>
            new(
                new DbContextOptionsBuilder<CleansiaDbContext>()
                    .UseNpgsql($"{_container.GetConnectionString()};Pooling=false")
                    .AddInterceptors(_interceptor)
                    .Options,
                new TestUserSessionProvider("system", "system@cleansia.test"),
                new NullTenantProvider());

        /// <summary>
        /// Thousands of rows INSIDE each partial filter but outside the date band — that is what forces
        /// the (Status, CurrentPeriodEnd) key to do the work rather than the partial filter alone — plus
        /// bulk outside both filters so a seq scan is genuinely the expensive option, and a handful
        /// inside each sweep window.
        /// </summary>
        private static async Task SeedSkewedDatasetAsync(NpgsqlConnection conn)
        {
            await Execute(conn, Insert("noise", 40000,
                periodEnd: $"'{Iso(Now.AddDays(20))}'",
                cancelledAt: "NULL",
                renewalSent: $"'{Iso(Now.AddDays(-1))}'",
                cancellationSent: "NULL"));

            await Execute(conn, Insert("pending", 8000,
                periodEnd: $"'{Iso(Now)}'::timestamptz - (1 + g % 365) * interval '1 day'",
                cancelledAt: $"'{Iso(Now.AddDays(-2))}'",
                renewalSent: "NULL",
                cancellationSent: "NULL"));

            await Execute(conn, Insert("unreminded", 6000,
                periodEnd: $"'{Iso(Now)}'::timestamptz + (30 + g % 300) * interval '1 day'",
                cancelledAt: "NULL",
                renewalSent: "NULL",
                cancellationSent: "NULL"));

            await Execute(conn, Insert("cancel-due", 5,
                periodEnd: $"'{Iso(Now.AddHours(12))}'",
                cancelledAt: $"'{Iso(Now.AddDays(-2))}'",
                renewalSent: $"'{Iso(Now.AddDays(-1))}'",
                cancellationSent: "NULL"));

            await Execute(conn, Insert("renew-due", 5,
                periodEnd: $"'{Iso(Now.AddDays(3))}'",
                cancelledAt: "NULL",
                renewalSent: "NULL",
                cancellationSent: "NULL"));
        }

        private static string Insert(
            string prefix, int count, string periodEnd, string cancelledAt,
            string renewalSent, string cancellationSent) =>
            "INSERT INTO \"UserMemberships\" " +
            "(\"Id\",\"UserId\",\"MembershipPlanId\",\"StripeSubscriptionId\",\"Status\"," +
            "\"CurrentPeriodStart\",\"CurrentPeriodEnd\",\"CancelledAt\",\"RenewalReminderSentAt\"," +
            "\"CancellationReminderSentAt\",\"IsActive\",\"CreatedBy\",\"CreatedOn\") SELECT " +
            $"'{prefix}-' || g, 'u-{prefix}-' || g, 'plan-1', 'sub-{prefix}-' || g, 1, " +
            $"'{Iso(Now.AddDays(-25))}', {periodEnd}, {cancelledAt}, {renewalSent}, " +
            $"{cancellationSent}, true, 'seed', '{Iso(Now)}' " +
            $"FROM generate_series(1, {count}) AS g;";

        private static async Task Execute(NpgsqlConnection conn, string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<int> Scalar(NpgsqlConnection conn, string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        private static string Iso(DateTime value) =>
            value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);

        private sealed class NoOpNotificationProducer : INotificationProducer
        {
            public Task NotifyAsync(
                string userId, string eventKey, Dictionary<string, string> args, string? tenantId,
                string? subject, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class NullTenantProvider : ITenantProvider
        {
            public string? GetCurrentTenantId() => null;
            public void SetTenantOverride(string tenantId) { }
            public void ClearTenantOverride() { }
        }
    }

    public sealed record CapturedStatement(string Sql, string Plan);

    /// <summary>
    /// EXPLAINs each sweep SELECT on the same connection, transaction and parameter values. The
    /// handler's per-row stamping UPDATEs are ignored.
    /// </summary>
    private sealed class PlanCapturingInterceptor : DbCommandInterceptor
    {
        public List<CapturedStatement> Statements { get; } = [];

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await RecordAsync(command, cancellationToken);
            return result;
        }

        private async Task RecordAsync(DbCommand command, CancellationToken ct)
        {
            if (!command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.Ordinal)
                || !command.CommandText.Contains("\"UserMemberships\"", StringComparison.Ordinal))
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
