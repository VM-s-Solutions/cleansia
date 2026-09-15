using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.EntityConfigurations;
using Cleansia.Infra.Database.Migrations;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// The committed <c>Initial</c> migration — the DDL that builds the DEV database — carries the tenancy
/// contract the model tests assert on the EF model: one <c>FK_&lt;T&gt;_Tenants_TenantId</c> per stamped
/// table, Restrict, NOT NULL on all but the two envelopes, and no <c>TenantId</c> column or
/// <c>IX_&lt;T&gt;_TenantId</c> index on a tenantless table. The model can drift ahead of the migration
/// between regenerations, so this reads the migration's own operations, not the model, and pins the
/// counts by hand: a new stamped table is a deliberate act and updates the number here.
/// </summary>
public sealed class InitialMigrationTenantDdlTests : IDisposable
{
    private const int StampedTables = 48;
    private const int EnvelopeTables = 2;

    private readonly SqliteConnection _connection;

    public InitialMigrationTenantDdlTests()
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
            options, new TestUserSessionProvider("system", "system@cleansia.test"), new FixedTenantProvider());
    }

    private sealed class FixedTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => TestTenants.Default;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }

    private static IReadOnlyList<CreateTableOperation> Tables() =>
        new Initial().UpOperations.OfType<CreateTableOperation>().ToList();

    private static IReadOnlyList<CreateIndexOperation> Indexes() =>
        new Initial().UpOperations.OfType<CreateIndexOperation>().ToList();

    private static bool IsTenantForeignKey(AddForeignKeyOperation fk) =>
        fk.PrincipalTable == "Tenants" && fk.Columns.SequenceEqual(new[] { nameof(ITenantEntity.TenantId) });

    private (HashSet<string> Stamped, HashSet<string> Envelopes) StampedTableNames()
    {
        using var ctx = NewContext();
        var stamped = new HashSet<string>(StringComparer.Ordinal);
        var envelopes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entity in ctx.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entity.ClrType))
            {
                continue;
            }

            stamped.Add(entity.GetTableName()!);
            if (TenantAuditableEntityConfiguration<TenantAuditable, string>.TenantIdNullableTypes.Contains(entity.ClrType))
            {
                envelopes.Add(entity.GetTableName()!);
            }
        }

        return (stamped, envelopes);
    }

    [Fact]
    public void Every_Stamped_Table_Is_Created_With_A_Restrict_Foreign_Key_Into_Tenants()
    {
        var (stamped, envelopes) = StampedTableNames();
        var tables = Tables().ToDictionary(t => t.Name, StringComparer.Ordinal);

        var offenders = new List<string>();
        foreach (var table in stamped.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!tables.TryGetValue(table, out var operation))
            {
                offenders.Add($"{table}: not created by the migration — regenerate Initial");
                continue;
            }

            var column = operation.Columns.SingleOrDefault(c => c.Name == nameof(ITenantEntity.TenantId));
            if (column is null)
            {
                offenders.Add($"{table}: no TenantId column");
                continue;
            }

            if (column.IsNullable != envelopes.Contains(table))
            {
                offenders.Add($"{table}: TenantId nullable = {column.IsNullable}");
            }

            var foreignKey = operation.ForeignKeys.SingleOrDefault(IsTenantForeignKey);
            if (foreignKey is null)
            {
                offenders.Add($"{table}: no foreign key into Tenants on TenantId");
                continue;
            }

            if (foreignKey.Name != $"FK_{table}_Tenants_TenantId")
            {
                offenders.Add($"{table}: FK is named {foreignKey.Name}");
            }

            if (foreignKey.OnDelete != ReferentialAction.Restrict)
            {
                offenders.Add($"{table}: FK is {foreignKey.OnDelete}, not Restrict");
            }
        }

        Assert.True(offenders.Count == 0,
            "The committed Initial migration disagrees with the tenancy contract on these stamped tables:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void No_Tenantless_Table_Is_Created_With_A_TenantId_Column_Or_Index()
    {
        var (stamped, _) = StampedTableNames();

        var columns = Tables()
            .Where(t => !stamped.Contains(t.Name))
            .Where(t => t.Columns.Any(c => c.Name == nameof(ITenantEntity.TenantId)) || t.ForeignKeys.Any(IsTenantForeignKey))
            .Select(t => t.Name)
            .ToList();

        var indexes = Indexes()
            .Where(ix => ix.Columns.SequenceEqual(new[] { nameof(ITenantEntity.TenantId) }) && !stamped.Contains(ix.Table))
            .Select(ix => ix.Name)
            .ToList();

        Assert.True(columns.Count == 0,
            "These tenantless tables are created with a TenantId column or a tenant FK:\n  " + string.Join("\n  ", columns));
        Assert.True(indexes.Count == 0,
            "These IX_<T>_TenantId indexes are created on tenantless tables:\n  " + string.Join("\n  ", indexes));
    }

    /// <summary>
    /// The literal count, so a stamped table silently demoted to plain <see cref="Auditable"/> — which the
    /// model-derived sweeps above cannot see, both sides agreeing — is caught here.
    /// </summary>
    [Fact]
    public void The_Migration_Carries_Exactly_The_Known_Number_Of_Tenant_Foreign_Keys()
    {
        var tenantForeignKeys = Tables()
            .SelectMany(t => t.ForeignKeys.Where(IsTenantForeignKey).Select(fk => (Table: t, ForeignKey: fk)))
            .ToList();

        Assert.Equal(StampedTables, tenantForeignKeys.Count);
        Assert.Equal(
            EnvelopeTables,
            tenantForeignKeys.Count(x => x.Table.Columns.Single(c => c.Name == nameof(ITenantEntity.TenantId)).IsNullable));
        Assert.All(tenantForeignKeys, x => Assert.EndsWith("_Tenants_TenantId", x.ForeignKey.Name));
    }
}
