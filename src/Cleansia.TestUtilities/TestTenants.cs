using Cleansia.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.TestUtilities;

/// <summary>
/// The operating companies the test fixtures stamp rows with. Every stamped table is NOT NULL
/// (ADR-0061 D8) and a foreign key into <c>Tenants</c>, so a fixture that commits an ITenantEntity
/// names one of these; the default is the seed's.
/// </summary>
public static class TestTenants
{
    public const string Default = "cleansia-cz";
    public const string Second = "cleansia-sk";

    /// <summary>
    /// Creates the schema and registers both companies in it, the way the seed script and the Postgres
    /// fixtures insert <c>Tenants</c> before anything that points at it. A fresh SQLite schema has no
    /// registry row, and the first stamped row a test inserts fails its foreign key without one.
    /// </summary>
    public static async Task EnsureCreatedWithRegistryAsync(DbContext context)
    {
        if (!await context.Database.EnsureCreatedAsync())
        {
            return;
        }

        context.AddRange(
            Tenant.Create(Default, "Cleansia CZ s.r.o."),
            Tenant.Create(Second, "Cleansia SK s.r.o."));
        await context.SaveChangesAsync();
    }
}
