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
/// <c>FiscalCounterAllocatorTests</c>, minus the two properties this counter deliberately does not
/// inherit: it is tenant-global, and it does NOT join the caller's transaction.
/// </summary>
[Collection("PostgresCollection")]
public class PayoutReferenceAllocatorTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const int Year = 2026;

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));
    }

    private static PayoutReferenceCounterRepository NewRepository(CleansiaDbContext context) =>
        new(context, new TestUserSessionProvider("system", "system@cleansia.test"));

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
    }

    [Fact]
    public async Task N_Concurrent_Allocations_Yield_N_Distinct_Contiguous_Ordinals()
    {
        await ResetAsync();

        const int concurrency = 25;

        var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            await using var context = NewContext();
            return await NewRepository(context).AllocateNextAsync(Year, CancellationToken.None);
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
            Assert.Equal(1, await NewRepository(context).AllocateNextAsync(Year, CancellationToken.None));
        }

        await using (var context = NewContext())
        {
            var repository = NewRepository(context);
            Assert.Equal(2, await repository.AllocateNextAsync(Year, CancellationToken.None));
            context.Rollback();
        }

        await using (var context = NewContext())
        {
            Assert.Equal(3, await NewRepository(context).AllocateNextAsync(Year, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Distinct_Years_Do_Not_Share_A_Sequence()
    {
        await ResetAsync();

        await using var context = NewContext();
        var repository = NewRepository(context);

        var first2026 = await repository.AllocateNextAsync(2026, CancellationToken.None);
        var first2027 = await repository.AllocateNextAsync(2027, CancellationToken.None);
        var second2026 = await repository.AllocateNextAsync(2026, CancellationToken.None);

        Assert.Equal(1, first2026);
        Assert.Equal(1, first2027);
        Assert.Equal(2, second2026);
    }

    [Fact]
    public async Task An_Exhausted_Year_Returns_No_Ordinal_Rather_Than_Wrapping()
    {
        await ResetAsync();
        await SeedCounterAtAsync(999999);

        await using var context = NewContext();

        var allocated = await NewRepository(context).AllocateNextAsync(Year, CancellationToken.None);

        Assert.Null(allocated);
        Assert.Equal(999999, await CurrentValueAsync());
    }

    [Fact]
    public async Task An_Exhausted_Year_Fails_With_Reference_Capacity_Exhausted_Rather_Than_Throwing()
    {
        await ResetAsync();
        await SeedCounterAtAsync(999999);

        await using var context = NewContext();
        var allocator = new PayoutReferenceAllocator(NewRepository(context));

        var result = await allocator.AllocateAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvoiceReferenceCapacityExhausted, result.Error!.Message);
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

    /// <summary>
    /// The counter row's shape, asserted against the DATABASE rather than the C# builder call: the key
    /// is (Year) and nothing else, and there is no TenantId column to re-arm the nulls-distinct trap.
    /// </summary>
    [Fact]
    public async Task The_Counter_Table_Is_Keyed_On_Year_Alone_And_Carries_No_Tenant_Column()
    {
        await ResetAsync();

        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();

        await using (var columns = conn.CreateCommand())
        {
            columns.CommandText =
                """
                SELECT column_name FROM information_schema.columns
                WHERE table_name = 'PayoutReferenceCounters'
                """;
            var names = new List<string>();
            await using var reader = await columns.ExecuteReaderAsync();
            while (await reader.ReadAsync()) names.Add(reader.GetString(0));

            Assert.DoesNotContain("TenantId", names);
            Assert.DoesNotContain("Scope", names);
            Assert.Contains("Year", names);
        }

        await using var index = conn.CreateCommand();
        index.CommandText =
            """
            SELECT indexdef FROM pg_indexes
            WHERE tablename = 'PayoutReferenceCounters' AND indexname = 'IX_PayoutReferenceCounters_Year'
            """;
        var definition = (string?)await index.ExecuteScalarAsync();

        Assert.NotNull(definition);
        Assert.Contains("UNIQUE", definition);
        // One column between the parentheses — a key with any second column is the failure mode.
        Assert.Single(Regex.Match(definition!, @"\(([^)]*)\)").Groups[1].Value.Split(','));
    }

    private async Task SeedCounterAtAsync(long value)
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        await using var command = conn.CreateCommand();
        command.CommandText =
            """
            INSERT INTO "PayoutReferenceCounters" ("Id", "Year", "Value", "IsActive", "CreatedBy", "CreatedOn")
            VALUES (@id, @year, @value, TRUE, 'system', NOW())
            """;
        command.Parameters.AddWithValue("id", Ulid.NewUlid().ToString());
        command.Parameters.AddWithValue("year", Year);
        command.Parameters.AddWithValue("value", value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long?> CurrentValueAsync()
    {
        await using var context = NewContext();
        var counter = await context.Set<PayoutReferenceCounter>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Year == Year);
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
