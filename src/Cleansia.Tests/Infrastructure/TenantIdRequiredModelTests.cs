using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.DeadLettering;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.EntityConfigurations;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// Every stamped table's <c>TenantId</c> column is NOT NULL (ADR-0061 D8) and a foreign key into
/// <c>Tenants</c>, and no other table carries the column at all. The failure this guards against is
/// silent: a writer that forgets its market, or names one that is not an operating company, lands rows
/// no tenanted reader can see and no error reports. Roster-free on purpose — it walks every
/// <see cref="ITenantEntity"/> in the model, so the next stamped table is covered the day it is added,
/// and an entity mapped by its own <c>IEntityTypeConfiguration</c> that skips the shared
/// TenantAuditable mapping goes red here rather than as a <c>text NULL</c> column in the DDL.
/// </summary>
public sealed class TenantIdRequiredModelTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public TenantIdRequiredModelTests()
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

    [Fact]
    public void Every_Stamped_Table_Has_A_Required_TenantId_Column_Capped_At_26()
    {
        using var ctx = NewContext();

        var offenders = new List<string>();
        var walked = 0;

        foreach (var entity in ctx.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entity.ClrType)
                || TenantAuditableEntityConfiguration<TenantAuditable, string>.TenantIdNullableTypes.Contains(entity.ClrType))
            {
                continue;
            }

            walked++;
            var property = entity.FindProperty(nameof(ITenantEntity.TenantId));
            if (property is null)
            {
                offenders.Add($"{entity.ClrType.Name}: TenantId is not mapped");
                continue;
            }

            if (property.IsNullable)
            {
                offenders.Add($"{entity.ClrType.Name}: TenantId is nullable");
            }

            if (property.GetMaxLength() != 26)
            {
                offenders.Add($"{entity.ClrType.Name}: TenantId max length is {property.GetMaxLength()?.ToString() ?? "unbounded"}, not 26");
            }
        }

        Assert.True(walked >= 40, $"Only {walked} stamped entities were walked; the roster-free walk is not seeing the model.");
        Assert.True(offenders.Count == 0,
            "TenantId is NOT NULL varchar(26) on every stamped table (ADR-0061 D8). Map these through "
            + "TenantAuditableEntityConfiguration or declare the column required explicitly:\n  "
            + string.Join("\n  ", offenders.OrderBy(x => x, StringComparer.Ordinal)));
    }

    /// <summary>
    /// The registry is a real constraint: every stamped table's <c>TenantId</c> is a foreign key into
    /// <c>Tenants</c> that refuses to cascade, so a tenant id that is not an operating company fails
    /// <c>23503</c> on first use and a company is never deleted out from under its rows.
    /// </summary>
    [Fact]
    public void Every_Stamped_Table_Is_A_Foreign_Key_Into_Tenants_That_Refuses_To_Cascade()
    {
        using var ctx = NewContext();

        var offenders = new List<string>();
        var walked = 0;

        foreach (var entity in ctx.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entity.ClrType))
            {
                continue;
            }

            walked++;
            var foreignKey = entity.GetForeignKeys().SingleOrDefault(fk =>
                fk.PrincipalEntityType.ClrType == typeof(Tenant)
                && fk.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ITenantEntity.TenantId) }));

            if (foreignKey is null)
            {
                offenders.Add($"{entity.ClrType.Name}: TenantId is not a foreign key into Tenants");
                continue;
            }

            if (foreignKey.DeleteBehavior is not DeleteBehavior.Restrict)
            {
                offenders.Add($"{entity.ClrType.Name}: FK to Tenants is {foreignKey.DeleteBehavior}, not Restrict");
            }

            if (foreignKey.DependentToPrincipal is not null || foreignKey.PrincipalToDependent is not null)
            {
                offenders.Add($"{entity.ClrType.Name}: the FK to Tenants carries a navigation; nothing in production reads Tenants");
            }
        }

        Assert.True(walked >= 40, $"Only {walked} stamped entities were walked; the roster-free walk is not seeing the model.");
        Assert.True(offenders.Count == 0,
            "Every stamped table points at the Tenants registry with a Restrict foreign key. Map these "
            + "through TenantAuditableEntityConfiguration or declare the relationship explicitly:\n  "
            + string.Join("\n  ", offenders.OrderBy(x => x, StringComparer.Ordinal)));
    }

    /// <summary>
    /// The column lives on <see cref="TenantAuditable"/>, not <see cref="Auditable"/>: a catalogue or
    /// per-country table has no tenant column, so nothing can stamp one by accident and no dead
    /// <c>IX_&lt;T&gt;_TenantId</c> index is emitted for it.
    /// </summary>
    [Fact]
    public void No_Tenantless_Table_Carries_A_TenantId_Column()
    {
        using var ctx = NewContext();

        Assert.Null(typeof(Auditable).GetProperty(nameof(ITenantEntity.TenantId)));

        var offenders = new List<string>();
        var walked = 0;

        foreach (var entity in ctx.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entity.ClrType))
            {
                continue;
            }

            walked++;
            if (entity.FindProperty(nameof(ITenantEntity.TenantId)) is not null)
            {
                offenders.Add(entity.ClrType.Name);
            }
        }

        Assert.True(walked >= 20, $"Only {walked} tenantless entities were walked; the roster-free walk is not seeing the model.");
        Assert.True(offenders.Count == 0,
            "These tables are not ITenantEntity yet carry a TenantId column. Either the type belongs on "
            + "TenantAuditable, or the column is dead and must go:\n  "
            + string.Join("\n  ", offenders.OrderBy(x => x, StringComparer.Ordinal)));
    }

    /// <summary>
    /// The negative control: the two infra tables that carry a tenant only when the envelope has one.
    /// Without this the walk above could pass on a reader that answered "required" for everything.
    /// </summary>
    [Theory]
    [InlineData(typeof(OutboxMessage))]
    [InlineData(typeof(DeadLetter))]
    public void The_Two_Envelope_Tables_Keep_A_Nullable_TenantId(Type exempt)
    {
        using var ctx = NewContext();

        Assert.Contains(exempt, TenantAuditableEntityConfiguration<TenantAuditable, string>.TenantIdNullableTypes);
        var property = ctx.Model.FindEntityType(exempt)!.FindProperty(nameof(ITenantEntity.TenantId))!;
        Assert.True(property.IsNullable, $"{exempt.Name}.TenantId is nullable by design: a poison body or a context with no tenant has none to give.");
    }

    /// <summary>
    /// The exemption list may only name stamped types — an entry that outlives its interface turns the
    /// list into fiction.
    /// </summary>
    [Fact]
    public void The_Exemption_List_Names_Only_Stamped_Types()
    {
        Assert.All(
            TenantAuditableEntityConfiguration<TenantAuditable, string>.TenantIdNullableTypes,
            type => Assert.True(typeof(ITenantEntity).IsAssignableFrom(type), $"{type.Name} is exempted but is not an ITenantEntity."));
    }
}
