using System.Net;
using System.Net.Http.Json;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// T-0290 AC1/AC6 — the new single-row audit read (AdminAuditLogController.GetAdminActionAuditById,
/// the seam that surfaces the snapshot blobs the paged cut withholds) end-to-end against the real Admin
/// host auth/authz pipeline:
/// <list type="bullet">
///   <item>a non-admin caller (right Admin audience, Employee/Customer role) is 403'd at the
///   <c>[Permission(CanViewAuditLog)]</c> gate and never reaches the handler — the privileged snapshot
///   surface is never served to a non-admin;</item>
///   <item>a cross-tenant Administrator asking for a row by its exact id is REJECTED (the EF global
///   query filter hides it → business not-found), never the other tenant's snapshot;</item>
///   <item>the in-tenant Administrator clears the same gate AND sees the row's snapshot (200), proving
///   the gate is genuinely enforced and the single-row read returns the blobs.</item>
/// </list>
/// </summary>
public sealed class AuditLogByIdViewPolicyTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";

    private static string Route(string auditId) => $"/api/AdminAuditLog/get-by-id/{auditId}";

    private static AdminActionAudit Audit(string id, string? tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        ActorId = "admin-1",
        ActorEmail = "admin-1@hosttests.local",
        ActorProfile = UserProfile.Administrator,
        Action = "IssuePartialRefund",
        ResourceType = "Order",
        ResourceId = "order-1",
        Success = true,
        OccurredOn = DateTimeOffset.UtcNow,
        BeforeJson = "{\"orderId\":\"order-1\",\"consumedRefund\":0}",
        AfterJson = "{\"orderId\":\"order-1\",\"consumedRefund\":500}",
    };

    [Fact]
    public async Task NonAdmin_Employee_is_403d_on_single_row_audit_read()
    {
        await SeedAsync(ctx =>
        {
            ctx.AdminActionAudits.Add(Audit("aud-a1", TenantA));
            return Task.CompletedTask;
        });

        var token = TestJwtFactory.Mint(AdminAudience, "emp-1", "emp-1@hosttests.local", UserProfile.Employee);

        var resp = await AdminClient(token).GetAsync(Route("aud-a1"));

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task NonAdmin_Customer_is_403d_on_single_row_audit_read()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "cust-1", "cust-1@hosttests.local", UserProfile.Customer);

        var resp = await AdminClient(token).GetAsync(Route("aud-a1"));

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task Anonymous_caller_is_rejected_on_single_row_audit_read()
    {
        var resp = await AdminHost.CreateClient().GetAsync(Route("aud-a1"));

        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task CrossTenant_Admin_is_rejected_and_never_served_the_other_tenants_row()
    {
        await SeedAsync(ctx =>
        {
            ctx.AdminActionAudits.Add(Audit("aud-a1", TenantA));
            return Task.CompletedTask;
        });

        // A genuine Administrator, but whose tenant is B, asking for the tenant-A row by its exact id.
        var token = TestJwtFactory.Mint(AdminAudience, "admin-b", "admin-b@hosttests.local",
            UserProfile.Administrator, tenantId: TenantB);

        var resp = await AdminClient(token).GetAsync(Route("aud-a1"));

        await HttpAssert.RejectedAsync(resp, Cleansia.Core.AppServices.Common.BusinessErrorMessage.AuditNotFound);
    }

    [Fact]
    public async Task InTenant_Admin_clears_the_gate_and_gets_the_row_with_its_snapshots()
    {
        await SeedAsync(ctx =>
        {
            ctx.AdminActionAudits.Add(Audit("aud-a1", TenantA));
            return Task.CompletedTask;
        });

        var token = TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local",
            UserProfile.Administrator, tenantId: TenantA);

        var resp = await AdminClient(token).GetAsync(Route("aud-a1"));

        HttpAssert.IsOk(resp);
        var dto = await resp.Content.ReadFromJsonAsync<AuditDetailResponse>();
        Assert.NotNull(dto);
        Assert.Equal("aud-a1", dto!.Id);
        // The snapshot blobs the paged cut withholds ARE present on the single-row read. jsonb round-trips
        // with Postgres' own whitespace, so assert on the parsed values, not byte-equal text.
        AssertConsumedRefund(dto.BeforeJson, 0);
        AssertConsumedRefund(dto.AfterJson, 500);
    }

    private static void AssertConsumedRefund(string? json, int expected)
    {
        Assert.NotNull(json);
        using var doc = System.Text.Json.JsonDocument.Parse(json!);
        Assert.Equal(expected, doc.RootElement.GetProperty("consumedRefund").GetInt32());
    }

    private sealed record AuditDetailResponse(string Id, string? BeforeJson, string? AfterJson);
}
