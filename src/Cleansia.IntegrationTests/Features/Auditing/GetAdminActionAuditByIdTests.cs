using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Auditing;

/// <summary>
/// AC1/AC2 over a REAL Postgres (Testcontainers): the single-row GetAdminActionAuditById query returns
/// ONE audit row by id INCLUDING its pre-redacted BeforeJson/AfterJson snapshot, and is TENANT-SCOPED by
/// the EF global query filter — a row stamped tenant A is invisible to a tenant-B reader, so a
/// cross-tenant id returns the not-found BusinessResult (never the other tenant's snapshot). The handler
/// runs against a production-shaped DbContext + repository so the tenant filter translates to SQL the
/// prod way.
/// </summary>
[Collection("PostgresCollection")]
public class GetAdminActionAuditByIdTests : BaseIntegrationTest
{
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";

    public GetAdminActionAuditByIdTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    private CleansiaDbContext NewContext(string? tenantId)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("admin-reader", "admin-reader@cleansia.test"),
            new FixedTenantProvider(tenantId));
    }

    private static Task<BusinessResult<AdminActionAuditDetailDto>> Handle(CleansiaDbContext ctx, string auditId)
    {
        IAdminActionAuditRepository repository = new AdminActionAuditRepository(ctx);
        var handlerType = typeof(GetAdminActionAuditById).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, repository)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<BusinessResult<AdminActionAuditDetailDto>>)method.Invoke(
            handler, [new GetAdminActionAuditById.Query(auditId), CancellationToken.None])!;
    }

    private static AdminActionAudit Audit(string id, string? tenantId)
    {
        return new AdminActionAudit
        {
            Id = id,
            TenantId = tenantId,
            ActorId = "admin-1",
            ActorEmail = "admin-1@cleansia.test",
            ActorProfile = UserProfile.Administrator,
            Action = "IssuePartialRefund",
            ResourceType = "Order",
            ResourceId = "order-1",
            Success = true,
            OccurredOn = DateTimeOffset.UtcNow,
            BeforeJson = "{\"orderId\":\"order-1\",\"consumedRefund\":0}",
            AfterJson = "{\"orderId\":\"order-1\",\"consumedRefund\":500}",
        };
    }

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

    private async Task SeedAsync(params AdminActionAudit[] rows)
    {
        await using var ctx = NewContext(tenantId: null);
        ctx.AddRange(rows);
        await ctx.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Returns_The_Row_With_Its_Snapshots_When_In_Tenant()
    {
        await ResetAsync();
        await SeedAsync(Audit("aud-a1", TenantA));

        await using var ctx = NewContext(tenantId: TenantA);
        var result = await Handle(ctx, "aud-a1");

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal("aud-a1", dto.Id);
        // jsonb round-trips with Postgres' own whitespace normalization, so compare parsed values, not
        // byte-equal text — the contract is "the stored snapshot survives the single-row read".
        AssertJsonField(dto.BeforeJson, "consumedRefund", 0);
        AssertJsonField(dto.AfterJson, "consumedRefund", 500);
    }

    private static void AssertJsonField(string? json, string field, int expected)
    {
        Assert.NotNull(json);
        using var doc = System.Text.Json.JsonDocument.Parse(json!);
        Assert.Equal(expected, doc.RootElement.GetProperty(field).GetInt32());
    }

    [Fact]
    public async Task CrossTenant_Id_Returns_NotFound_Not_The_Other_Tenants_Snapshot()
    {
        await ResetAsync();
        await SeedAsync(Audit("aud-a1", TenantA), Audit("aud-b1", TenantB));

        // A tenant-B reader asks for a tenant-A row by its exact id: the global query filter hides it,
        // so the handler returns not-found — never the cross-tenant snapshot.
        await using var ctx = NewContext(tenantId: TenantB);
        var result = await Handle(ctx, "aud-a1");

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.AuditNotFound, result.Error!.Message);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
