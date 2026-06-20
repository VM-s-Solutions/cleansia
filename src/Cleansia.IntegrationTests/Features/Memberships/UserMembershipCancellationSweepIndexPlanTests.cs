using System.Globalization;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Cleansia.IntegrationTests.Features.Memberships;

/// <summary>
/// Query-plan proof (optimizer gate) that the cancellation-reminder arm of the membership lifecycle
/// sweep is index-served. The renewal arm already had a partial index
/// <c>(Status, CurrentPeriodEnd) WHERE RenewalReminderSentAt IS NULL</c>; that filter does NOT cover
/// the cancellation arm's predicate (CancelledAt IS NOT NULL AND CancellationReminderSentAt IS NULL),
/// so the sweep fell to a seq scan. This pins the new partial index
/// <c>IX_UserMemberships_Status_CurrentPeriodEnd_Cancellation</c> as the plan EXPLAIN actually picks.
///
/// Uses its OWN throwaway Postgres container with the schema built from the CURRENT EF model
/// (<c>EnsureCreated</c> — so <c>OnModelCreating</c> emits the new index), independent of the shared
/// migrated fixture: the owner-applied ef-migration for this index does not exist yet, so the model is
/// the source of truth here. FK constraints on UserMemberships are dropped so rows can be inserted
/// directly (the test isolates index selection, not the User/MembershipPlan FKs) — the same isolation
/// the SQLite unique-index test makes with Foreign Keys=False.
/// </summary>
public sealed class UserMembershipCancellationSweepIndexPlanTests : IAsyncLifetime
{
    private const string CancellationIndexName = "IX_UserMemberships_Status_CurrentPeriodEnd_Cancellation";
    private const string RenewalIndexName = "IX_UserMemberships_Status_CurrentPeriodEnd";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:latest")
        .WithDatabase("plandb")
        .WithUsername("planuser")
        .WithPassword("planpass")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        await using var ctx = new CleansiaDbContext(options);
        await ctx.Database.EnsureCreatedAsync();

        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();

        // Isolate index selection: drop the two Restrict FKs so rows insert without a full User /
        // MembershipPlan graph. The constraint names are EF's defaults from the model.
        await ExecuteAsync(conn,
            "ALTER TABLE \"UserMemberships\" DROP CONSTRAINT IF EXISTS \"FK_UserMemberships_Users_UserId\";" +
            "ALTER TABLE \"UserMemberships\" DROP CONSTRAINT IF EXISTS \"FK_UserMemberships_MembershipPlans_MembershipPlanId\";");

        await SeedSkewedDatasetAsync(conn);

        // The planner needs stats to prefer the tiny partial index over a seq scan.
        await ExecuteAsync(conn, "ANALYZE \"UserMemberships\";");
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    // ── the cancellation-sweep predicate is served by the new partial index, not a seq scan ──

    [Fact]
    public async Task CancellationSweepPredicate_UsesCancellationPartialIndex_NotSeqScan()
    {
        var plan = await ExplainCancellationSweepAsync();

        Assert.Contains(CancellationIndexName, plan, StringComparison.Ordinal);
        Assert.DoesNotContain("Seq Scan on \"UserMemberships\"", plan, StringComparison.Ordinal);
    }

    // ── the renewal arm's plan is unchanged: it still uses the renewal partial index (no regression) ──

    [Fact]
    public async Task RenewalSweepPredicate_StillUsesRenewalPartialIndex()
    {
        var plan = await ExplainRenewalSweepAsync();

        Assert.Contains(RenewalIndexName, plan, StringComparison.Ordinal);
        Assert.DoesNotContain(CancellationIndexName, plan, StringComparison.Ordinal);
    }

    private async Task<string> ExplainCancellationSweepAsync()
    {
        var now = DateTime.UtcNow;
        var windowEnd = now.AddDays(2);
        return await ExplainAsync(
            "EXPLAIN SELECT * FROM \"UserMemberships\" " +
            "WHERE \"CancelledAt\" IS NOT NULL " +
            "AND \"CancellationReminderSentAt\" IS NULL " +
            "AND \"Status\" = 1 " +
            $"AND \"CurrentPeriodEnd\" >= '{Iso(now)}' " +
            $"AND \"CurrentPeriodEnd\" <= '{Iso(windowEnd)}';");
    }

    private async Task<string> ExplainRenewalSweepAsync()
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddDays(2);
        var windowEnd = now.AddDays(4);
        return await ExplainAsync(
            "EXPLAIN SELECT * FROM \"UserMemberships\" " +
            "WHERE \"Status\" = 1 " +
            "AND \"RenewalReminderSentAt\" IS NULL " +
            $"AND \"CurrentPeriodEnd\" >= '{Iso(windowStart)}' " +
            $"AND \"CurrentPeriodEnd\" <= '{Iso(windowEnd)}';");
    }

    private async Task<string> ExplainAsync(string explainSql)
    {
        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(explainSql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(0));
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Skew the table so the planner strongly prefers the partial index: thousands of rows OUTSIDE the
    /// cancellation predicate (active, no pending cancellation) and only a handful INSIDE it. A seq scan
    /// over the bulk would be far costlier than the tiny partial index.
    /// </summary>
    private static async Task SeedSkewedDatasetAsync(NpgsqlConnection conn)
    {
        var now = DateTime.UtcNow;

        // Bulk noise: active rows with NO pending cancellation (CancelledAt null) — excluded by both
        // partial filters, so they bloat a seq scan but never enter either index.
        await ExecuteAsync(conn,
            "INSERT INTO \"UserMemberships\" " +
            "(\"Id\",\"UserId\",\"MembershipPlanId\",\"StripeSubscriptionId\",\"Status\"," +
            "\"CurrentPeriodStart\",\"CurrentPeriodEnd\",\"CancelledAt\",\"RenewalReminderSentAt\"," +
            "\"CancellationReminderSentAt\",\"IsActive\",\"CreatedBy\",\"CreatedOn\") " +
            "SELECT " +
            "'noise-' || g, 'u-' || g, 'plan-' || g, 'sub-' || g, 1, " +
            $"'{Iso(now.AddDays(-10))}', '{Iso(now.AddDays(20))}', NULL, '{Iso(now.AddDays(-1))}', " +
            $"NULL, true, 'seed', '{Iso(now)}' " +
            "FROM generate_series(1, 5000) AS g;");

        // The few rows that match the cancellation-sweep predicate: pending cancellation, not yet
        // reminded, active, ending inside [now, now+2d].
        await ExecuteAsync(conn,
            "INSERT INTO \"UserMemberships\" " +
            "(\"Id\",\"UserId\",\"MembershipPlanId\",\"StripeSubscriptionId\",\"Status\"," +
            "\"CurrentPeriodStart\",\"CurrentPeriodEnd\",\"CancelledAt\",\"RenewalReminderSentAt\"," +
            "\"CancellationReminderSentAt\",\"IsActive\",\"CreatedBy\",\"CreatedOn\") " +
            "SELECT " +
            "'cancel-' || g, 'cu-' || g, 'plan-c', 'sub-c-' || g, 1, " +
            $"'{Iso(now.AddDays(-25))}', '{Iso(now.AddHours(12))}', '{Iso(now.AddDays(-2))}', NULL, " +
            $"NULL, true, 'seed', '{Iso(now)}' " +
            "FROM generate_series(1, 5) AS g;");

        // A few rows for the renewal arm: active, not yet renewal-reminded, ending inside [now+2d, now+4d].
        await ExecuteAsync(conn,
            "INSERT INTO \"UserMemberships\" " +
            "(\"Id\",\"UserId\",\"MembershipPlanId\",\"StripeSubscriptionId\",\"Status\"," +
            "\"CurrentPeriodStart\",\"CurrentPeriodEnd\",\"CancelledAt\",\"RenewalReminderSentAt\"," +
            "\"CancellationReminderSentAt\",\"IsActive\",\"CreatedBy\",\"CreatedOn\") " +
            "SELECT " +
            "'renew-' || g, 'ru-' || g, 'plan-r', 'sub-r-' || g, 1, " +
            $"'{Iso(now.AddDays(-27))}', '{Iso(now.AddDays(3))}', NULL, NULL, " +
            $"NULL, true, 'seed', '{Iso(now)}' " +
            "FROM generate_series(1, 5) AS g;");
    }

    private static async Task ExecuteAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string Iso(DateTime value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
}
