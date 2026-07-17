using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Token minting resolves the employee_id claim by user email on the anonymous login + refresh paths,
/// where no tenant claim exists yet. The tenant-SCOPED <see cref="EmployeeRepository.GetByUserEmailAsync"/>
/// narrows to <c>TenantId == null</c> there, so a tenant-stamped employee would be missed and the JWT
/// minted WITHOUT employee_id (T-0361). These pin that the tenant-IGNORING lookup added for those paths
/// finds the tenant-stamped employee, while the tenant-scoped read (as used with a null-tenant context)
/// does not — reproducing the bug and its fix against a real <see cref="CleansiaDbContext"/> over SQLite.
/// </summary>
public sealed class EmployeeRepositoryTenantTokenLookupTests : IDisposable
{
    private const string Tenant = "tenant-emp-1";
    private const string TenantEmail = "stamped.cleaner@cleansia.test";
    private const string TenantUserId = "user-emp-tenant";
    private const string EmployeeId = "emp-tenant-1";

    private readonly SqliteConnection _connection;

    public EmployeeRepositoryTenantTokenLookupTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Employee carries FKs (nationality/address/country) we don't seed here; the tenant-filter
        // behaviour under test is independent of them, so disable FK enforcement like the other
        // employee-seeding repo tests.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext(string? tenantId) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));

    /// <summary>Seeds one employee committed under a tenant-carrying context, so CommitAsync stamps its
    /// TenantId — exactly what a migrated multi-tenant cleaner account looks like.</summary>
    private async Task SeedTenantStampedEmployeeAsync()
    {
        await using (var ctx = NewContext(tenantId: null))
        {
            await ctx.Database.EnsureCreatedAsync();
        }

        await using (var ctx = NewContext(Tenant))
        {
            var user = User.CreateWithPassword(TenantEmail, "Passw0rd!", "Stamped", "Cleaner", UserProfile.Employee);
            user.Id = TenantUserId;
            var employee = Employee.CreateWithUser(user);
            employee.Id = EmployeeId;
            ctx.Add(employee);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var verify = NewContext(tenantId: null);
        var stamped = await verify.Set<Employee>().IgnoreQueryFilters().FirstAsync(e => e.Id == EmployeeId);
        Assert.Equal(Tenant, stamped.TenantId);
    }

    [Fact]
    public async Task GetByUserEmailIgnoringTenantAsync_AnonymousTokenPath_FindsTheTenantStampedEmployee()
    {
        await SeedTenantStampedEmployeeAsync();

        await using var ctx = NewContext(tenantId: null);
        var employee = await new EmployeeRepository(ctx)
            .GetByUserEmailIgnoringTenantAsync(TenantEmail, CancellationToken.None);

        Assert.NotNull(employee);
        Assert.Equal(EmployeeId, employee!.Id);
    }

    [Fact]
    public async Task GetByUserEmailAsync_TenantScoped_MissesTheTenantStampedEmployee_OnANullTenantContext()
    {
        // The bug (T-0361): the tenant-scoped read on a tenant-less token path returns null, so the token
        // would be minted without employee_id — which is exactly why the minting paths switched to the
        // IgnoringTenant lookup above.
        await SeedTenantStampedEmployeeAsync();

        await using var ctx = NewContext(tenantId: null);
        var employee = await new EmployeeRepository(ctx)
            .GetByUserEmailAsync(TenantEmail, CancellationToken.None);

        Assert.Null(employee);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
