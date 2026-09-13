using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.DeadLettering;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.EntityConfigurations;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// Every stamped table's <c>TenantId</c> column is NOT NULL (ADR-0061 D8). The failure this guards
/// against is silent: a writer that forgets its market lands rows no tenanted reader can see and no
/// error reports. Roster-free on purpose — it walks every <see cref="ITenantEntity"/> in the model, so
/// the next stamped table is covered the day it is added, and an entity mapped by its own
/// <c>IEntityTypeConfiguration</c> that skips the shared Auditable mapping goes red here rather than
/// as a <c>text NULL</c> column in the DDL.
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
                || AuditableEntityConfiguration<Auditable, string>.TenantIdNullableTypes.Contains(entity.ClrType))
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
            + "AuditableEntityConfiguration or declare the column required explicitly:\n  "
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

        Assert.Contains(exempt, AuditableEntityConfiguration<Auditable, string>.TenantIdNullableTypes);
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
            AuditableEntityConfiguration<Auditable, string>.TenantIdNullableTypes,
            type => Assert.True(typeof(ITenantEntity).IsAssignableFrom(type), $"{type.Name} is exempted but is not an ITenantEntity."));
    }
}
