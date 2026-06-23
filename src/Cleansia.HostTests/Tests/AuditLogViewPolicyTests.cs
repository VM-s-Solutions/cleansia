using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0012 D7 / T-0285 — the admin audit-log read surface (AdminAuditLogController) lives on the
/// Admin host, gated by the new <c>Policy.CanViewAuditLog = AdminOnly</c>. End-to-end against the real
/// host auth/authz pipeline:
/// <list type="bullet">
///   <item>a non-admin caller (right Admin audience, Employee/Customer role) is 403'd at the
///   [Permission] gate and never reaches the handler — the privileged accountability log is never
///   served to a non-admin;</item>
///   <item>the Administrator-role companion clears the same gate (200), proving it is genuinely
///   enforced rather than open.</item>
/// </list>
/// </summary>
public sealed class AuditLogViewPolicyTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string Route = "/api/AdminAuditLog/get-paged";

    [Fact]
    public async Task NonAdmin_Employee_is_403d_on_audit_log_read()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "emp-1", "emp-1@hosttests.local", UserProfile.Employee);

        var resp = await AdminClient(token).GetAsync(Route);

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task NonAdmin_Customer_is_403d_on_audit_log_read()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "cust-1", "cust-1@hosttests.local", UserProfile.Customer);

        var resp = await AdminClient(token).GetAsync(Route);

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task Anonymous_caller_is_rejected_on_audit_log_read()
    {
        var resp = await AdminHost.CreateClient().GetAsync(Route);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Admin_caller_clears_the_audit_log_read_gate()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "admin-1", "admin-1@hosttests.local", UserProfile.Administrator);

        var resp = await AdminClient(token).GetAsync(Route);

        HttpAssert.IsOk(resp);
    }
}
