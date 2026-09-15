using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// The payout-reference allocator over a REAL Postgres DbContext (Testcontainers) — SQLite cannot
/// model the row-lock / <c>RETURNING</c> concurrency the whole design rests on. The direct analogue of
/// <c>FiscalCounterAllocatorTests</c>: keyed per operating company like the fiscal counter, minus the
/// one property this counter deliberately does not inherit — it does NOT join the caller's transaction.
/// </summary>
[Collection("PostgresCollection")]
public class PayoutReferenceAllocatorTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const int Year = 2026;

    private const string VariableSymbol = PayoutReferenceCounter.VariableSymbolScope;
    private const string InvoiceNumber = PayoutReferenceCounter.InvoiceNumberScope;

    private CleansiaDbContext NewContext(string? tenantId = TestTenants.Default)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));
    }

    private static PayoutReferenceCounterRepository NewRepository(CleansiaDbContext context, string? tenantId = TestTenants.Default) =>
        new(context, new FixedTenantProvider(tenantId), new TestUserSessionProvider("system", "system@cleansia.test"));

    private async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        });
        await respawner.ResetAsync(conn);
        await SeedTenantRegistryAsync(conn);
    }

    [Fact]
    public async Task N_Concurrent_Allocations_Yield_N_Distinct_Contiguous_Ordinals()
    {
        await ResetAsync();

        const int concurrency = 25;

        var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            await using var context = NewContext();
            return await NewRepository(context).AllocateNextAsync(Year, VariableSymbol, CancellationToken.None);
        }).ToArray();

        var allocated = await Task.WhenAll(tasks);

        Assert.All(allocated, value => Assert.NotNull(value));
        Assert.Equal(concurrency, allocated.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, concurrency).Select(i => (long?)i), allocated.OrderBy(v => v));
    }

    /// <summary>
    /// The property that separates this counter from <c>FiscalCounter</c>: the statement auto-commits,
    /// so a caller whose own work rolls back leaves a GAP rather than returning the number to the pool.
    /// A payment reference is not a fiscal document number, and a design that sometimes gaps and never
    /// duplicates beats one that never gaps and sometimes duplicates.
    /// </summary>
    [Fact]
    public async Task An_Allocation_Whose_Caller_Rolls_Back_Leaves_A_Gap()
    {
        await ResetAsync();

        await using (var context = NewContext())
        {
            Assert.Equal(1, await NewRepository(context).AllocateNextAsync(Year, VariableSymbol, CancellationToken.None));
        }

        await using (var context = NewContext())
        {
            var repository = NewRepository(context);
            Assert.Equal(2, await repository.AllocateNextAsync(Year, VariableSymbol, CancellationToken.None));
            context.Rollback();
        }

        await using (var context = NewContext())
        {
            Assert.Equal(3, await NewRepository(context).AllocateNextAsync(Year, VariableSymbol, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Distinct_Years_Do_Not_Share_A_Sequence()
    {
        await ResetAsync();

        await using var context = NewContext();
        var repository = NewRepository(context);

        var first2026 = await repository.AllocateNextAsync(2026, VariableSymbol, CancellationToken.None);
        var first2027 = await repository.AllocateNextAsync(2027, VariableSymbol, CancellationToken.None);
        var second2026 = await repository.AllocateNextAsync(2026, VariableSymbol, CancellationToken.None);

        Assert.Equal(1, first2026);
        Assert.Equal(1, first2027);
        Assert.Equal(2, second2026);
    }

    /// <summary>
    /// The variable symbol and the invoice number are two series, not one number printed two ways: the
    /// owner ruled they may never coincide, and independent ordinals are what keeps a gap in one from
    /// shifting the other.
    /// </summary>
    [Fact]
    public async Task The_Two_Scopes_Do_Not_Share_A_Sequence()
    {
        await ResetAsync();

        await using var context = NewContext();
        var repository = NewRepository(context);

        var firstSymbol = await repository.AllocateNextAsync(Year, VariableSymbol, CancellationToken.None);
        var firstNumber = await repository.AllocateNextAsync(Year, InvoiceNumber, CancellationToken.None);
        var secondSymbol = await repository.AllocateNextAsync(Year, VariableSymbol, CancellationToken.None);

        Assert.Equal(1, firstSymbol);
        Assert.Equal(1, firstNumber);
        Assert.Equal(2, secondSymbol);
    }

    /// <summary>
    /// The 2026-09-15 ruling: each operating company numbers its own payout invoices. Two companies'
    /// first references of a year are the SAME ordinal, and one company's allocations never advance
    /// the other's row.
    /// </summary>
    [Fact]
    public async Task Two_Companies_Allocate_Independent_Sequences()
    {
        await ResetAsync();

        await using var context = NewContext();
        var first = NewRepository(context, TestTenants.Default);
        var second = NewRepository(context, TestTenants.Second);

        Assert.Equal(1, await first.AllocateNextAsync(Year, VariableSymbol, CancellationToken.None));
        Assert.Equal(2, await first.AllocateNextAsync(Year, VariableSymbol, CancellationToken.None));
        Assert.Equal(1, await second.AllocateNextAsync(Year, VariableSymbol, CancellationToken.None));
        Assert.Equal(3, await first.AllocateNextAsync(Year, VariableSymbol, CancellationToken.None));
        Assert.Equal(2, await second.AllocateNextAsync(Year, VariableSymbol, CancellationToken.None));
    }

    /// <summary>
    /// With no ambient company the row has no tenant to carry and the NOT NULL column refuses it —
    /// a numbering run outside any company fails loudly instead of quietly sharing one holding-wide
    /// sequence that no company's reader would ever see.
    /// </summary>
    [Fact]
    public async Task An_Allocation_With_No_Ambient_Company_Is_Refused_By_The_Database()
    {
        await ResetAsync();

        await using var context = NewContext(tenantId: null);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            NewRepository(context, tenantId: null).AllocateNextAsync(Year, VariableSymbol, CancellationToken.None));

        Assert.Equal(PostgresErrorCodes.NotNullViolation, ex.SqlState);
    }

    [Fact]
    public async Task An_Exhausted_Year_Returns_No_Ordinal_Rather_Than_Wrapping()
    {
        await ResetAsync();
        await SeedCounterAtAsync(TestTenants.Default, VariableSymbol, 999999);

        await using var context = NewContext();

        var allocated = await NewRepository(context).AllocateNextAsync(Year, VariableSymbol, CancellationToken.None);

        Assert.Null(allocated);
        Assert.Equal(999999, await CurrentValueAsync(TestTenants.Default, VariableSymbol));
    }

    /// <summary>The cap is per company: one company's exhausted year is not another's.</summary>
    [Fact]
    public async Task One_Companys_Exhausted_Year_Does_Not_Exhaust_Anothers()
    {
        await ResetAsync();
        await SeedCounterAtAsync(TestTenants.Default, VariableSymbol, 999999);

        await using var context = NewContext();

        Assert.Null(await NewRepository(context, TestTenants.Default).AllocateNextAsync(Year, VariableSymbol, CancellationToken.None));
        Assert.Equal(1, await NewRepository(context, TestTenants.Second).AllocateNextAsync(Year, VariableSymbol, CancellationToken.None));
    }

    [Fact]
    public async Task An_Exhausted_Year_Fails_With_Reference_Capacity_Exhausted_Rather_Than_Throwing()
    {
        await ResetAsync();
        await SeedCounterAtAsync(TestTenants.Default, VariableSymbol, 999999);
        await SeedCounterAtAsync(TestTenants.Default, InvoiceNumber, 999999);

        await using var context = NewContext();
        var allocator = new PayoutReferenceAllocator(NewRepository(context));

        var symbol = await allocator.AllocateAsync(CancellationToken.None);
        var number = await allocator.AllocateInvoiceNumberAsync(CancellationToken.None);

        Assert.True(symbol.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvoiceReferenceCapacityExhausted, symbol.Error!.Message);
        Assert.True(number.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvoiceReferenceCapacityExhausted, number.Error!.Message);
    }

    [Fact]
    public async Task Every_Allocated_Symbol_Is_Ten_Digits_And_Never_Starts_With_Zero()
    {
        await ResetAsync();

        await using var context = NewContext();
        var allocator = new PayoutReferenceAllocator(NewRepository(context));

        for (var i = 0; i < 5; i++)
        {
            var result = await allocator.AllocateAsync(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Matches("^[1-9][0-9]{9}$", result.Value!);
        }
    }

    [Fact]
    public async Task Every_Allocated_Invoice_Number_Is_INV_The_Year_And_Six_Digits()
    {
        await ResetAsync();

        await using var context = NewContext();
        var allocator = new PayoutReferenceAllocator(NewRepository(context));

        for (var i = 1; i <= 5; i++)
        {
            var result = await allocator.AllocateInvoiceNumberAsync(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal($"INV-{DateTime.UtcNow.Year:D4}-{i:D6}", result.Value);
        }
    }

    /// <summary>
    /// The counter row's shape, asserted against the DATABASE rather than the C# builder call: the key
    /// is (TenantId, Year, Scope), unique, and NULLS NOT DISTINCT — the shape every sole-arbiter tenant
    /// index carries, so a null in the arbiter can never insert a second row for one series.
    /// </summary>
    [Fact]
    public async Task The_Counter_Table_Is_Keyed_Per_Company_Year_And_Scope()
    {
        await ResetAsync();

        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();

        await using (var columns = conn.CreateCommand())
        {
            columns.CommandText =
                """
                SELECT column_name, is_nullable FROM information_schema.columns
                WHERE table_name = 'PayoutReferenceCounters'
                """;
            var nullability = new Dictionary<string, string>(StringComparer.Ordinal);
            await using var reader = await columns.ExecuteReaderAsync();
            while (await reader.ReadAsync()) nullability[reader.GetString(0)] = reader.GetString(1);

            Assert.Equal("NO", nullability["TenantId"]);
            Assert.Equal("NO", nullability["Year"]);
            Assert.Equal("NO", nullability["Scope"]);
        }

        await using var index = conn.CreateCommand();
        index.CommandText =
            """
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'PayoutReferenceCounters' AND indexname = 'IX_PayoutReferenceCounters_Tenant_Year_Scope'
            """;
        var definition = (string?)await index.ExecuteScalarAsync();

        Assert.NotNull(definition);
        Assert.Contains("UNIQUE", definition);
        Assert.Contains("NULLS NOT DISTINCT", definition);
        Assert.Equal(
            ["\"TenantId\"", "\"Year\"", "\"Scope\""],
            Regex.Match(definition!, @"\(([^)]*)\)").Groups[1].Value.Split(',').Select(c => c.Trim()));
    }

    private async Task SeedCounterAtAsync(string tenantId, string scope, long value)
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        await using var command = conn.CreateCommand();
        command.CommandText =
            """
            INSERT INTO "PayoutReferenceCounters" ("Id", "TenantId", "Year", "Scope", "Value", "IsActive", "CreatedBy", "CreatedOn")
            VALUES (@id, @tenantId, @year, @scope, @value, TRUE, 'system', NOW())
            """;
        command.Parameters.AddWithValue("id", Ulid.NewUlid().ToString());
        command.Parameters.AddWithValue("tenantId", tenantId);
        command.Parameters.AddWithValue("year", Year);
        command.Parameters.AddWithValue("scope", scope);
        command.Parameters.AddWithValue("value", value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long?> CurrentValueAsync(string tenantId, string scope)
    {
        await using var context = NewContext();
        var counter = await context.Set<PayoutReferenceCounter>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Year == Year && c.Scope == scope);
        return counter?.Value;
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
