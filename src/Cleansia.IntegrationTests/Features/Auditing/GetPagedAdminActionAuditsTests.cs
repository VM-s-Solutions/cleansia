using System.Reflection;
using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Features.Auditing.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Auditing;

/// <summary>
/// TC-AUDIT-QUERY (ADR-0012 D7) over a REAL Postgres (Testcontainers): the canonical
/// GetPagedAdminActionAudits query filters by actor / action / resource / date-range / outcome and is
/// TENANT-SCOPED by the EF global query filter — a row stamped tenant A is invisible to a tenant-B
/// admin reader. The handler runs against a production-shaped DbContext + repository (the same
/// construction the refresh-token tenant test uses) so the tenant filter translates to SQL the prod way.
/// </summary>
[Collection("PostgresCollection")]
public class GetPagedAdminActionAuditsTests : BaseIntegrationTest
{
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";

    public GetPagedAdminActionAuditsTests(PostgresContainerFixture fixture) : base(fixture)
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

    private static Task<PagedData<AdminActionAuditDto>> Handle(CleansiaDbContext ctx, GetPagedAdminActionAudits.Request request)
    {
        IAdminActionAuditRepository repository = new AdminActionAuditRepository(ctx);
        var handlerType = typeof(GetPagedAdminActionAudits).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, repository)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<AdminActionAuditDto>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static AdminActionAudit Audit(
        string id,
        string? tenantId,
        string actorId = "admin-1",
        string action = "IssuePartialRefund",
        string resourceType = "Order",
        string resourceId = "order-1",
        bool success = true,
        DateTimeOffset? occurredOn = null)
    {
        return new AdminActionAudit
        {
            Id = id,
            TenantId = tenantId,
            ActorId = actorId,
            ActorEmail = $"{actorId}@cleansia.test",
            ActorProfile = UserProfile.Administrator,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Success = success,
            ErrorCode = success ? null : "refund.exceeds_remaining",
            OccurredOn = occurredOn ?? DateTimeOffset.UtcNow,
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
        // Seed ignoring the tenant filter: each row carries its own explicit TenantId so the
        // CommitAsync auto-stamp (which only fills a null TenantId) does not override it.
        await using var ctx = NewContext(tenantId: null);
        ctx.AddRange(rows);
        await ctx.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TenantScoped_TenantB_Reader_Cannot_See_TenantA_Rows()
    {
        await ResetAsync();
        await SeedAsync(
            Audit("aud-a1", TenantA),
            Audit("aud-a2", TenantA),
            Audit("aud-b1", TenantB));

        await using var ctx = NewContext(tenantId: TenantB);
        var page = await Handle(ctx, new GetPagedAdminActionAudits.Request());

        Assert.Equal(1, page.Total);
        var row = Assert.Single(page.Data);
        Assert.Equal("aud-b1", row.Id);
    }

    [Fact]
    public async Task Filter_By_Actor_Returns_Only_That_Actor()
    {
        await ResetAsync();
        await SeedAsync(
            Audit("aud-1", TenantA, actorId: "admin-1"),
            Audit("aud-2", TenantA, actorId: "admin-2"));

        await using var ctx = NewContext(tenantId: TenantA);
        var page = await Handle(ctx, new GetPagedAdminActionAudits.Request
        {
            Filter = new AdminActionAuditFilter("admin-2", null, null, null, null, null, null, null)
        });

        Assert.Equal(1, page.Total);
        Assert.Equal("aud-2", Assert.Single(page.Data).Id);
    }

    [Fact]
    public async Task Filter_By_Resource_And_Action_Returns_Matching_History()
    {
        await ResetAsync();
        await SeedAsync(
            Audit("aud-1", TenantA, action: "IssuePartialRefund", resourceType: "Order", resourceId: "order-1"),
            Audit("aud-2", TenantA, action: "AdminOverrideOrderStatus", resourceType: "Order", resourceId: "order-1"),
            Audit("aud-3", TenantA, action: "IssuePartialRefund", resourceType: "Order", resourceId: "order-2"));

        await using var ctx = NewContext(tenantId: TenantA);
        var byResource = await Handle(ctx, new GetPagedAdminActionAudits.Request
        {
            Filter = new AdminActionAuditFilter(null, null, null, "Order", "order-1", null, null, null)
        });
        Assert.Equal(2, byResource.Total);

        var byActionAndResource = await Handle(ctx, new GetPagedAdminActionAudits.Request
        {
            Filter = new AdminActionAuditFilter(null, null, "IssuePartialRefund", "Order", "order-1", null, null, null)
        });
        Assert.Equal(1, byActionAndResource.Total);
        Assert.Equal("aud-1", Assert.Single(byActionAndResource.Data).Id);
    }

    [Fact]
    public async Task Filter_By_Outcome_Returns_Only_Failures()
    {
        await ResetAsync();
        await SeedAsync(
            Audit("aud-ok", TenantA, success: true),
            Audit("aud-fail", TenantA, success: false));

        await using var ctx = NewContext(tenantId: TenantA);
        var page = await Handle(ctx, new GetPagedAdminActionAudits.Request
        {
            Filter = new AdminActionAuditFilter(null, null, null, null, null, null, null, Success: false)
        });

        Assert.Equal(1, page.Total);
        var row = Assert.Single(page.Data);
        Assert.Equal("aud-fail", row.Id);
        Assert.False(row.Success);
        Assert.Equal("refund.exceeds_remaining", row.ErrorCode);
    }

    [Fact]
    public async Task Filter_By_DateRange_And_Default_Order_Is_NewestFirst()
    {
        await ResetAsync();
        var june = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var july = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);
        var august = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
        await SeedAsync(
            Audit("aud-june", TenantA, occurredOn: june),
            Audit("aud-july", TenantA, occurredOn: july),
            Audit("aud-august", TenantA, occurredOn: august));

        await using var ctx = NewContext(tenantId: TenantA);

        var dateRange = await Handle(ctx, new GetPagedAdminActionAudits.Request
        {
            Filter = new AdminActionAuditFilter(null, null, null, null, null,
                OccurredFrom: new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
                OccurredTo: new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.Zero),
                Success: null)
        });
        Assert.Equal(1, dateRange.Total);
        Assert.Equal("aud-july", Assert.Single(dateRange.Data).Id);

        var all = await Handle(ctx, new GetPagedAdminActionAudits.Request());
        var ids = all.Data.Select(d => d.Id).ToList();
        Assert.Equal(new[] { "aud-august", "aud-july", "aud-june" }, ids);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
