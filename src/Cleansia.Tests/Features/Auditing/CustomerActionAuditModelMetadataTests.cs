using System.Reflection;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D1/D5/D7 — the customer table's shape in the EF model, the <c>AdminActionAuditModelMetadataTests</c>
/// twin: tenant-scoped and NOT auditable, append-only with exactly one sanctioned mutator, the four
/// indexes the reads and the retention sweep need, and a jsonb payload.
/// </summary>
public sealed class CustomerActionAuditModelMetadataTests
{
    private static IEntityType GetEntityType()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var ctx = new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

        var entityType = ctx.Model.FindEntityType(typeof(CustomerActionAudit));
        Assert.NotNull(entityType);
        return entityType!;
    }

    private static bool HasIndexOn(IEntityType entityType, params string[] columns) =>
        entityType.GetIndexes().Any(ix => ix.Properties.Select(p => p.Name).SequenceEqual(columns));

    [Fact]
    public void ExtendsBaseEntity_AndIsTenantScoped_NotAuditable()
    {
        Assert.True(typeof(BaseEntity).IsAssignableFrom(typeof(CustomerActionAudit)));
        Assert.True(typeof(ITenantEntity).IsAssignableFrom(typeof(CustomerActionAudit)));
        Assert.False(typeof(Auditable).IsAssignableFrom(typeof(CustomerActionAudit)));
    }

    [Fact]
    public void IsSealed()
    {
        Assert.True(typeof(CustomerActionAudit).IsSealed);
    }

    /// <summary>
    /// No public setter on anything the entity declares: the row is built by its factory and, from then
    /// on, only <see cref="CustomerActionAudit.Pseudonymise"/> writes to it. <c>TenantId</c> is the one
    /// exception — <c>ITenantEntity</c> requires a setter for the writer/sink stamp.
    /// </summary>
    [Fact]
    public void AppendOnly_NoDeclaredPropertyHasAPublicSetter()
    {
        var offenders = typeof(CustomerActionAudit)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.Name != nameof(ITenantEntity.TenantId))
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ThePseudonymiseMutatorIsTheOnlyPublicInstanceMethod()
    {
        var mutators = typeof(CustomerActionAudit)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        Assert.Equal([nameof(CustomerActionAudit.Pseudonymise)], mutators);
    }

    [Fact]
    public void Pseudonymise_BlanksExactlyTheThreeRequestMetadataColumns()
    {
        var row = CustomerActionAudit.Create(
            userId: "cust-1", clientAudience: "cleansia.customer", ipAddress: "203.0.113.9",
            deviceLabel: "iPhone", deviceId: "dev-1", action: "customer.order.cancel", resourceType: "Order",
            resourceId: "ORD-1", success: true, errorCode: null, payloadJson: "{\"feeRate\":0.5}", correlationId: "corr-1");

        row.Pseudonymise();

        Assert.Null(row.IpAddress);
        Assert.Null(row.DeviceLabel);
        Assert.Null(row.DeviceId);
        Assert.Equal("cust-1", row.UserId);
        Assert.Equal("cleansia.customer", row.ClientAudience);
        Assert.Equal("customer.order.cancel", row.Action);
        Assert.Equal("Order", row.ResourceType);
        Assert.Equal("ORD-1", row.ResourceId);
        Assert.True(row.Success);
        Assert.Equal("{\"feeRate\":0.5}", row.PayloadJson);
        Assert.Equal("corr-1", row.CorrelationId);
    }

    [Fact]
    public void IsMappedToTheCustomerActionAuditsTable()
    {
        Assert.Equal("CustomerActionAudits", GetEntityType().GetTableName());
    }

    [Fact]
    public void HasTenantGlobalQueryFilter_ThatNeverMentionsIsActive()
    {
        var filters = GetEntityType().GetDeclaredQueryFilters();

        Assert.NotEmpty(filters);
        Assert.All(filters, f => Assert.DoesNotContain(nameof(BaseEntity.IsActive), f.Expression?.ToString() ?? string.Empty));
    }

    [Fact]
    public void TenantId_IsRequired_WithMaxLength26()
    {
        var property = GetEntityType().FindProperty(nameof(CustomerActionAudit.TenantId));
        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
        Assert.Equal(26, property.GetMaxLength());
    }

    [Fact]
    public void UserId_IsNullable_ForGuestActs_AndHasNoForeignKey()
    {
        var entityType = GetEntityType();
        var property = entityType.FindProperty(nameof(CustomerActionAudit.UserId));

        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
        Assert.Equal(26, property.GetMaxLength());
        Assert.Empty(entityType.GetForeignKeys());
        Assert.Empty(entityType.GetNavigations());
    }

    [Theory]
    [InlineData(nameof(CustomerActionAudit.ClientAudience), 40)]
    [InlineData(nameof(CustomerActionAudit.Action), 100)]
    public void RequiredColumns_AreNotNullable_AndCapped(string propertyName, int maxLength)
    {
        var property = GetEntityType().FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    [Theory]
    [InlineData(nameof(CustomerActionAudit.IpAddress), 45)]
    [InlineData(nameof(CustomerActionAudit.DeviceLabel), 120)]
    [InlineData(nameof(CustomerActionAudit.DeviceId), 64)]
    [InlineData(nameof(CustomerActionAudit.ResourceType), 50)]
    [InlineData(nameof(CustomerActionAudit.ResourceId), 26)]
    [InlineData(nameof(CustomerActionAudit.ErrorCode), 100)]
    [InlineData(nameof(CustomerActionAudit.CorrelationId), 64)]
    public void OptionalColumns_AreNullable_AndCapped(string propertyName, int maxLength)
    {
        var property = GetEntityType().FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    [Fact]
    public void PayloadJson_IsNullableJsonb()
    {
        var payload = GetEntityType().FindProperty(nameof(CustomerActionAudit.PayloadJson));

        Assert.NotNull(payload);
        Assert.True(payload!.IsNullable);
        Assert.Equal("jsonb", payload.GetColumnType());
    }

    [Fact]
    public void HasPagedFeedIndex_TenantId_OccurredOn()
    {
        Assert.True(HasIndexOn(GetEntityType(),
            nameof(CustomerActionAudit.TenantId), nameof(CustomerActionAudit.OccurredOn)));
    }

    [Fact]
    public void HasPerSubjectIndex_UserId_OccurredOn()
    {
        Assert.True(HasIndexOn(GetEntityType(),
            nameof(CustomerActionAudit.UserId), nameof(CustomerActionAudit.OccurredOn)));
    }

    [Fact]
    public void HasPerResourceIndex_ResourceType_ResourceId()
    {
        Assert.True(HasIndexOn(GetEntityType(),
            nameof(CustomerActionAudit.ResourceType), nameof(CustomerActionAudit.ResourceId)));
    }

    [Fact]
    public void HasRetentionScanIndex_OccurredOn()
    {
        Assert.True(HasIndexOn(GetEntityType(), nameof(CustomerActionAudit.OccurredOn)));
    }

    [Fact]
    public void HasExactlyTheFourIndexes_AndNoneOnAction()
    {
        var indexes = GetEntityType().GetIndexes().ToList();

        Assert.Equal(4, indexes.Count);
        Assert.DoesNotContain(indexes, ix => ix.Properties.Any(p => p.Name == nameof(CustomerActionAudit.Action)));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
