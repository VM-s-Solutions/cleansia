using System.Reflection;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cleansia.Tests.Features.Auditing;

public sealed class AdminActionAuditModelMetadataTests
{
    private static IEntityType GetEntityType()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var ctx = new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

        var entityType = ctx.Model.FindEntityType(typeof(AdminActionAudit));
        Assert.NotNull(entityType);
        return entityType!;
    }

    private static bool HasIndexOn(IEntityType entityType, params string[] columns) =>
        entityType.GetIndexes().Any(ix => ix.Properties.Select(p => p.Name).SequenceEqual(columns));

    [Fact]
    public void AdminActionAudit_ExtendsBaseEntity_AndIsTenantScoped_NotAuditable()
    {
        Assert.True(typeof(BaseEntity).IsAssignableFrom(typeof(AdminActionAudit)));
        Assert.True(typeof(ITenantEntity).IsAssignableFrom(typeof(AdminActionAudit)));
        Assert.False(typeof(Auditable).IsAssignableFrom(typeof(AdminActionAudit)));
    }

    [Fact]
    public void AdminActionAudit_IsSealed()
    {
        Assert.True(typeof(AdminActionAudit).IsSealed);
    }

    [Theory]
    [InlineData(nameof(AdminActionAudit.ActorId))]
    [InlineData(nameof(AdminActionAudit.ActorEmail))]
    [InlineData(nameof(AdminActionAudit.ActorProfile))]
    [InlineData(nameof(AdminActionAudit.Action))]
    [InlineData(nameof(AdminActionAudit.ResourceType))]
    [InlineData(nameof(AdminActionAudit.ResourceId))]
    [InlineData(nameof(AdminActionAudit.Success))]
    [InlineData(nameof(AdminActionAudit.ErrorCode))]
    [InlineData(nameof(AdminActionAudit.OccurredOn))]
    [InlineData(nameof(AdminActionAudit.Reason))]
    [InlineData(nameof(AdminActionAudit.BeforeJson))]
    [InlineData(nameof(AdminActionAudit.AfterJson))]
    [InlineData(nameof(AdminActionAudit.CorrelationId))]
    public void AdminActionAudit_AppendOnly_EverySettablePropertyIsInitOnly(string propertyName)
    {
        var property = typeof(AdminActionAudit).GetProperty(propertyName);
        Assert.NotNull(property);

        var setter = property!.SetMethod;
        Assert.NotNull(setter);

        var isInitOnly = setter!.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(t => t.FullName == "System.Runtime.CompilerServices.IsExternalInit");

        Assert.True(isInitOnly, $"{propertyName} must be init-only so AdminActionAudit is append-only.");
    }

    [Fact]
    public void AdminActionAudit_HasNoPublicMutationMethod()
    {
        var mutators = typeof(AdminActionAudit)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToList();

        Assert.Empty(mutators);
    }

    [Fact]
    public void AdminActionAudit_IsMappedToTheAdminActionAuditsTable()
    {
        Assert.Equal("AdminActionAudits", GetEntityType().GetTableName());
    }

    [Fact]
    public void AdminActionAudit_HasTenantGlobalQueryFilter()
    {
        Assert.NotNull(GetEntityType().GetQueryFilter());
    }

    [Fact]
    public void TenantId_IsNullable_WithMaxLength26()
    {
        var property = GetEntityType().FindProperty(nameof(AdminActionAudit.TenantId));
        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
        Assert.Equal(26, property.GetMaxLength());
    }

    [Fact]
    public void BeforeJson_AndAfterJson_AreNullableJsonb()
    {
        var before = GetEntityType().FindProperty(nameof(AdminActionAudit.BeforeJson));
        var after = GetEntityType().FindProperty(nameof(AdminActionAudit.AfterJson));

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.True(before!.IsNullable);
        Assert.True(after!.IsNullable);
        Assert.Equal("jsonb", before.GetColumnType());
        Assert.Equal("jsonb", after.GetColumnType());
    }

    [Theory]
    [InlineData(nameof(AdminActionAudit.ActorId))]
    [InlineData(nameof(AdminActionAudit.Action))]
    public void RequiredColumns_AreNotNullable(string propertyName)
    {
        var property = GetEntityType().FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
    }

    [Fact]
    public void HasPagedFeedIndex_TenantId_OccurredOn()
    {
        Assert.True(HasIndexOn(GetEntityType(),
            nameof(AdminActionAudit.TenantId), nameof(AdminActionAudit.OccurredOn)));
    }

    [Fact]
    public void HasPerResourceIndex_ResourceType_ResourceId()
    {
        Assert.True(HasIndexOn(GetEntityType(),
            nameof(AdminActionAudit.ResourceType), nameof(AdminActionAudit.ResourceId)));
    }

    [Fact]
    public void HasPerActorIndex_ActorId_OccurredOn()
    {
        Assert.True(HasIndexOn(GetEntityType(),
            nameof(AdminActionAudit.ActorId), nameof(AdminActionAudit.OccurredOn)));
    }

    [Fact]
    public void HasPerActionIndex_Action_OccurredOn()
    {
        Assert.True(HasIndexOn(GetEntityType(),
            nameof(AdminActionAudit.Action), nameof(AdminActionAudit.OccurredOn)));
    }

    [Fact]
    public void HasExactlyTheFourD6Indexes()
    {
        Assert.Equal(4, GetEntityType().GetIndexes().Count());
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
