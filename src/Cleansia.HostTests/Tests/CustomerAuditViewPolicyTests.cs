using System.Net.Http.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0062 D6 — the customer audit list (<c>CustomerAuditController.GetPagedCustomerActionAudits</c>)
/// lives on the Admin host behind <c>Policy.CanViewAuditLog</c>, end-to-end against the real auth/authz
/// pipeline: Employee and Customer roles are 403'd at the gate, an anonymous caller is 401'd, an
/// Administrator clears it, and the global tenant filter keeps another operator's rows out of the page.
/// </summary>
public sealed class CustomerAuditViewPolicyTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string Route = "/api/CustomerAudit/get-paged";

    [Fact]
    public async Task NonAdmin_Employee_is_403d_on_customer_audit_list()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "emp-1", "emp-1@hosttests.local", UserProfile.Employee);

        var resp = await AdminClient(token).GetAsync(Route);

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task NonAdmin_Customer_is_403d_on_customer_audit_list()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "cust-1", "cust-1@hosttests.local", UserProfile.Customer);

        var resp = await AdminClient(token).GetAsync(Route);

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task Anonymous_caller_is_401d_on_customer_audit_list()
    {
        var resp = await AdminHost.CreateClient().GetAsync(Route);

        HttpAssert.IsUnauthorized(resp);
    }

    [Fact]
    public async Task Admin_clears_the_gate_and_sees_only_their_own_tenants_rows()
    {
        await SeedAsync(ctx =>
        {
            ctx.CustomerActionAudits.AddRange(
                DomainSeed.CustomerAudit("caud-a1", "cust-a", HostTestTenants.A),
                DomainSeed.CustomerAudit("caud-b1", "cust-b", HostTestTenants.B));
            return Task.CompletedTask;
        });

        var token = TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local",
            UserProfile.Administrator, tenantId: HostTestTenants.A);

        var resp = await AdminClient(token).GetAsync(Route);

        HttpAssert.IsOk(resp);
        var page = await resp.Content.ReadFromJsonAsync<PageResponse>();
        Assert.NotNull(page);
        Assert.Equal(1, page!.Total);
        var row = Assert.Single(page.Data);
        Assert.Equal("caud-a1", row.Id);
    }

    private sealed record PageResponse(int Total, List<RowResponse> Data);

    private sealed record RowResponse(string Id, string? UserId);
}
