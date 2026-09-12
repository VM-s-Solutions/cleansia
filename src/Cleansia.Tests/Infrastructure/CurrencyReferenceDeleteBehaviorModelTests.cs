using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// A currency may not be deleted out from under anything denominated in it. Every table that carries a
/// CurrencyId declares <c>.OnDelete(DeleteBehavior.Restrict)</c> — except Orders, which for one
/// release did not declare the relationship at all and inherited EF Core's default for a REQUIRED
/// navigation: Cascade. Deleting a currency took the orders priced in it and the nine child tables
/// that cascade from an order.
///
/// <para>A hand-written roster fails OPEN here: a new entity that gains a CurrencyId is not weakly
/// covered by such a list, it is invisible to it — which is exactly how Orders was missed. So this
/// walks the model instead, and the second case pins that the walk actually sees something.</para>
/// </summary>
public sealed class CurrencyReferenceDeleteBehaviorModelTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CurrencyReferenceDeleteBehaviorModelTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options, new TestUserSessionProvider("system", "system@cleansia.test"), new NullTenantProvider());
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }

    private static List<(string Owner, string Property, DeleteBehavior Behavior)> CurrencyReferences(
        CleansiaDbContext ctx) =>
        ctx.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys()
                .Where(fk => fk.PrincipalEntityType.ClrType == typeof(Currency))
                .Select(fk => (
                    Owner: e.ClrType.Name,
                    Property: string.Join(", ", fk.Properties.Select(p => p.Name)),
                    Behavior: fk.DeleteBehavior)))
            .ToList();

    [Fact]
    public void Every_Reference_To_A_Currency_Refuses_To_Cascade()
    {
        using var ctx = NewContext();

        var offenders = CurrencyReferences(ctx)
            .Where(r => r.Behavior is not (DeleteBehavior.Restrict or DeleteBehavior.NoAction))
            .Select(r => $"{r.Owner}.{r.Property} -> {r.Behavior}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "A currency must not be deletable out from under money denominated in it. These "
            + "relationships would let a currency delete take their rows with it:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Anti-vacuity. Without this the sweep above passes on an empty set — a renamed type or a dropped
    /// navigation and it would silently stop looking at anything.
    /// </summary>
    [Fact]
    public void The_Sweep_Sees_The_Order_Currency_Relationship_It_Exists_For()
    {
        using var ctx = NewContext();
        var references = CurrencyReferences(ctx);

        Assert.True(references.Count >= 8,
            $"Expected at least the eight known CurrencyId foreign keys, saw {references.Count}.");
        Assert.Contains(references, r => r.Owner == "Order" && r.Property == "CurrencyId");
    }
}
