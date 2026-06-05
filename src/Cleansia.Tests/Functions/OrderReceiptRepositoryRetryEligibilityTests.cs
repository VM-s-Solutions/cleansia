using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0004 C-A — the born-retry-eligible claim must be SWEPT by the retry job.
///
/// <para>Under claim-before-register, a crash between the claim commit and the authority register
/// leaves a committed row with <c>FiscalRegistrationFailed == false, FiscalCode == null,
/// FiscalNextRetryAt != null</c>. The pre-widening <c>GetDueForRetryAsync</c> filtered on
/// <c>FiscalRegistrationFailed == true</c>, so this row was INVISIBLE to the retry job (RED).
/// ADR-0004 C-A mandates widening the query to ALSO return
/// <c>FiscalCode == null &amp;&amp; FiscalNextRetryAt != null &amp;&amp; FiscalNextRetryAt &lt;= utcNow</c>
/// regardless of the failed flag, so the claimed-but-unregistered row is recovered (GREEN).</para>
///
/// <para>Spins a REAL <see cref="CleansiaDbContext"/> over SQLite in-memory (so the model + the
/// partial filtered index <c>IX_OrderReceipts_FiscalNextRetryAt</c> actually materialize) — no
/// Postgres/Docker. Written TEST-FIRST (RED before the query widening).</para>
/// </summary>
public sealed class OrderReceiptRepositoryRetryEligibilityTests : IDisposable
{
    private const string LanguageId = "lang-cz";

    private readonly SqliteConnection _connection;

    public OrderReceiptRepositoryRetryEligibilityTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // This suite exercises ONLY the GetDueForRetryAsync predicate (C-A), not referential
        // integrity. The OrderReceipt → Order / → Language FKs are irrelevant here, so disable FK
        // enforcement on the shared in-memory connection to seed receipts without a full Order graph.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));
    }

    private async Task EnsureSchemaAndLanguageAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        if (!await ctx.Set<Language>().AnyAsync(l => l.Id == LanguageId))
        {
            var language = Language.Create("cz", "Czech");
            language.Id = LanguageId;
            ctx.Add(language);
            await ctx.CommitAsync(CancellationToken.None);
        }
    }

    private static OrderReceipt NewReceipt(string orderId, string receiptNumber)
        => OrderReceipt.Create(orderId, receiptNumber, $"{receiptNumber}.pdf", $"2026/{orderId}/{receiptNumber}.pdf", LanguageId);

    // ── the born-retry-eligible claimed-but-unregistered row IS returned (C-A) ──

    [Fact]
    public async Task AC_F4_4_Claimed_But_Unregistered_Row_Is_Returned_By_GetDueForRetry()
    {
        await EnsureSchemaAndLanguageAsync();

        await using (var seedCtx = NewContext())
        {
            // Crash between claim-commit and register: born retry-eligible (FiscalNextRetryAt set), but
            // NOT FiscalRegistrationFailed, and FiscalCode still null.
            var claimed = NewReceipt("01HZX9N6M7Q8R9S0T1V2W3X401", "2026-000001");
            claimed.ScheduleImmediateFiscalRetry();
            seedCtx.Add(claimed);
            await seedCtx.CommitAsync(CancellationToken.None);

            Assert.False(claimed.FiscalRegistrationFailed);
            Assert.Null(claimed.FiscalCode);
            Assert.NotNull(claimed.FiscalNextRetryAt);
        }

        await using var ctx = NewContext();
        var repo = new OrderReceiptRepository(ctx);
        var due = await repo.GetDueForRetryAsync(DateTime.UtcNow.AddMinutes(1), take: 50, CancellationToken.None);

        Assert.Single(due);
        Assert.Equal("2026-000001", due[0].ReceiptNumber);
    }

    // ── No regression — the existing FiscalRegistrationFailed==true row is still returned ──

    [Fact]
    public async Task AC_F4_4_Existing_Failed_Row_Still_Returned()
    {
        await EnsureSchemaAndLanguageAsync();

        await using (var seedCtx = NewContext())
        {
            var failed = NewReceipt("01HZX9N6M7Q8R9S0T1V2W3X402", "2026-000002");
            failed.MarkFiscalRegistrationFailed("cz-eet2", FiscalErrorKindForTransient(), "boom");
            seedCtx.Add(failed);
            await seedCtx.CommitAsync(CancellationToken.None);

            Assert.True(failed.FiscalRegistrationFailed);
            Assert.NotNull(failed.FiscalNextRetryAt);
        }

        await using var ctx = NewContext();
        var repo = new OrderReceiptRepository(ctx);
        var due = await repo.GetDueForRetryAsync(DateTime.UtcNow.AddHours(1), take: 50, CancellationToken.None);

        Assert.Single(due);
        Assert.Equal("2026-000002", due[0].ReceiptNumber);
    }

    // ── A successfully-registered row (FiscalCode set, no NextRetryAt) is NOT returned ──

    [Fact]
    public async Task AC_F4_4_Registered_Row_Is_Not_Returned()
    {
        await EnsureSchemaAndLanguageAsync();

        await using (var seedCtx = NewContext())
        {
            var registered = NewReceipt("01HZX9N6M7Q8R9S0T1V2W3X403", "2026-000003");
            registered.ScheduleImmediateFiscalRetry();           // claimed pending...
            registered.SetFiscalData("cz-eet2", "FIK-OK", DateTime.UtcNow); // ...then the authority signed it (clears NextRetryAt).
            seedCtx.Add(registered);
            await seedCtx.CommitAsync(CancellationToken.None);

            Assert.NotNull(registered.FiscalCode);
            Assert.Null(registered.FiscalNextRetryAt);
        }

        await using var ctx = NewContext();
        var repo = new OrderReceiptRepository(ctx);
        var due = await repo.GetDueForRetryAsync(DateTime.UtcNow.AddHours(1), take: 50, CancellationToken.None);

        Assert.Empty(due);
    }

    // The transient kind that MarkFiscalRegistrationFailed schedules a retry for.
    private static Cleansia.Core.Fiscal.Abstractions.FiscalErrorKind FiscalErrorKindForTransient()
        => Cleansia.Core.Fiscal.Abstractions.FiscalErrorKind.Transient;

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
