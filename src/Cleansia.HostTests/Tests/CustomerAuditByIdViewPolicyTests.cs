using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0062 D6 — the single-row customer audit read (<c>CustomerAuditController.GetCustomerActionAuditById</c>),
/// the one seam that serves the payload, IP address and device label, end-to-end against the real Admin
/// host pipeline: Employee and Customer roles are 403'd at the <c>[Permission(CanViewAuditLog)]</c> gate,
/// an anonymous caller is 401'd, a cross-tenant Administrator asking by exact id is refused as not-found
/// (the global tenant filter), and the in-tenant Administrator gets the row with its payload.
/// </summary>
public sealed class CustomerAuditByIdViewPolicyTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private static string Route(string auditId) => $"/api/CustomerAudit/get-by-id/{auditId}";

    [Fact]
    public async Task NonAdmin_Employee_is_403d_on_single_row_customer_audit_read()
    {
        await SeedAsync(ctx =>
        {
            ctx.CustomerActionAudits.Add(DomainSeed.CustomerAudit("caud-a1", "cust-a", HostTestTenants.A));
            return Task.CompletedTask;
        });

        var token = TestJwtFactory.Mint(AdminAudience, "emp-1", "emp-1@hosttests.local", UserProfile.Employee);

        var resp = await AdminClient(token).GetAsync(Route("caud-a1"));

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task NonAdmin_Customer_is_403d_on_single_row_customer_audit_read()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "cust-1", "cust-1@hosttests.local", UserProfile.Customer);

        var resp = await AdminClient(token).GetAsync(Route("caud-a1"));

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task Anonymous_caller_is_401d_on_single_row_customer_audit_read()
    {
        var resp = await AdminHost.CreateClient().GetAsync(Route("caud-a1"));

        HttpAssert.IsUnauthorized(resp);
    }

    [Fact]
    public async Task CrossTenant_Admin_is_refused_and_never_served_the_other_tenants_row()
    {
        await SeedAsync(ctx =>
        {
            ctx.CustomerActionAudits.Add(DomainSeed.CustomerAudit("caud-a1", "cust-a", HostTestTenants.A));
            return Task.CompletedTask;
        });

        var token = TestJwtFactory.Mint(AdminAudience, "admin-b", "admin-b@hosttests.local",
            UserProfile.Administrator, tenantId: HostTestTenants.B);

        var resp = await AdminClient(token).GetAsync(Route("caud-a1"));

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.AuditNotFound);
    }

    [Fact]
    public async Task InTenant_Admin_clears_the_gate_and_gets_the_row_with_its_payload_and_request_metadata()
    {
        await SeedAsync(ctx =>
        {
            ctx.CustomerActionAudits.Add(DomainSeed.CustomerAudit("caud-a1", "cust-a", HostTestTenants.A));
            return Task.CompletedTask;
        });

        var token = TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local",
            UserProfile.Administrator, tenantId: HostTestTenants.A);

        var resp = await AdminClient(token).GetAsync(Route("caud-a1"));

        HttpAssert.IsOk(resp);
        var dto = await resp.Content.ReadFromJsonAsync<DetailResponse>();
        Assert.NotNull(dto);
        Assert.Equal("caud-a1", dto!.Id);
        Assert.Equal("cust-a", dto.UserId);
        Assert.Equal("203.0.113.9", dto.IpAddress);
        Assert.Equal("iPhone 15 / iOS 17.4", dto.DeviceLabel);
        // jsonb round-trips with Postgres' own whitespace, so assert on the parsed value.
        Assert.NotNull(dto.PayloadJson);
        using var payload = JsonDocument.Parse(dto.PayloadJson!);
        Assert.Equal(0.5m, payload.RootElement.GetProperty("feeRate").GetDecimal());
    }

    private sealed record DetailResponse(string Id, string? UserId, string? IpAddress, string? DeviceLabel, string? PayloadJson);
}
