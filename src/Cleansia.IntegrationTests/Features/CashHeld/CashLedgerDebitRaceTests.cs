using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.CashHeld;

/// <summary>
/// Two writers taking the same cash off a cleaner at once, on Postgres - two administrators, or an invoice's
/// set-off and an administrator: the second debit waits for the first to commit, then reads the balance it
/// left and is refused, so the cash held never goes below zero.
/// </summary>
[Collection("PostgresCollection")]
public sealed class CashLedgerDebitRaceTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private const string EmployeeId = "emp-cash-race";
    private const string CurrencyId = "currency-czk-cash-race";
    private const decimal Held = 1500m;

    private NpgsqlDataSource _dataSource = default!;

    public async Task InitializeAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.GetConnectionString())
        {
            Database = "cash_ledger_debit_race_test"
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        _dataSource = builder.Build();

        await using (var bootstrap = NewContext())
        {
            await bootstrap.Database.EnsureDeletedAsync();
            await TestTenants.EnsureCreatedWithRegistryAsync(bootstrap);
        }

        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ReloadTypesAsync();

        // The subject is the balance, not the cleaner or the currency its rows point at.
        await using var seed = new NpgsqlCommand(
            """
            ALTER TABLE "CashLedgerEntries"
                DROP CONSTRAINT "FK_CashLedgerEntries_Employees_EmployeeId",
                DROP CONSTRAINT "FK_CashLedgerEntries_Currencies_CurrencyId";
            INSERT INTO "CashLedgerEntries"
                ("Id", "EmployeeId", "CurrencyId", "Kind", "Amount", "OccurredAt", "IsActive", "CreatedBy", "CreatedOn", "TenantId")
            VALUES ('cash-race-collected', @employeeId, @currencyId, @kind, @amount, now(), TRUE, 'seed', now(), @tenantId);
            """,
            conn);
        seed.Parameters.AddWithValue("employeeId", EmployeeId);
        seed.Parameters.AddWithValue("currencyId", CurrencyId);
        seed.Parameters.AddWithValue("kind", (int)CashLedgerEntryKind.Collection);
        seed.Parameters.AddWithValue("amount", Held);
        seed.Parameters.AddWithValue("tenantId", TestTenants.Default);
        await seed.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.EnsureDeletedAsync();
        }

        await _dataSource.DisposeAsync();
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_dataSource).Options,
            new TestUserSessionProvider("admin-cash-race", "admin-cash-race@cleansia.test"),
            new DefaultTenantProvider());

    [Fact]
    public async Task A_Write_Off_Racing_A_Remittance_Of_The_Whole_Cash_Held_Waits_For_It_And_Is_Refused_Once_It_Commits()
    {
        var now = DateTime.UtcNow;
        await using var remitting = NewContext();
        Assert.True(await new CashLedgerRepository(remitting).TryDebitAsync(
            CashLedgerEntry.ForRemittance(EmployeeId, CurrencyId, Held, null, now), CancellationToken.None));

        await using var writingOff = NewContext();
        var writeOff = new CashLedgerRepository(writingOff).TryDebitAsync(
            CashLedgerEntry.ForWriteOff(EmployeeId, CurrencyId, Held, "Unreachable", now), CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.False(writeOff.IsCompleted);

        await remitting.CommitAsync(CancellationToken.None);

        Assert.False(await writeOff);
        writingOff.Rollback();

        await using var read = NewContext();
        Assert.Equal(0m, await new CashLedgerRepository(read).GetHeldAsync(EmployeeId, CurrencyId, CancellationToken.None));
    }

    [Fact]
    public async Task A_Remittance_Racing_A_Set_Off_Of_The_Whole_Cash_Held_Waits_For_It_And_Is_Refused_Once_It_Commits()
    {
        await using var settingOff = NewContext();
        var ledger = new CashLedgerRepository(settingOff);
        var invoice = EmployeeInvoice.Create(
            EmployeeId, "period-cash-race", 1, 2000m, CurrencyId, "1", "INV-CASH-RACE");
        invoice.SetOffCash(await ledger.GetHeldUnderLockAsync(EmployeeId, CurrencyId, CancellationToken.None));
        ledger.Add(CashLedgerEntry.ForSetOff(invoice));

        await using var remitting = NewContext();
        var remittance = new CashLedgerRepository(remitting).TryDebitAsync(
            CashLedgerEntry.ForRemittance(EmployeeId, CurrencyId, Held, null, DateTime.UtcNow), CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.False(remittance.IsCompleted);

        await settingOff.CommitAsync(CancellationToken.None);

        Assert.False(await remittance);
        remitting.Rollback();

        await using var read = NewContext();
        Assert.Equal(0m, await new CashLedgerRepository(read).GetHeldAsync(EmployeeId, CurrencyId, CancellationToken.None));
    }

    private sealed class DefaultTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => TestTenants.Default;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
