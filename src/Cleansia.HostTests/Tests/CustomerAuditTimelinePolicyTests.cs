using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0062 D6 — the three-source timeline (<c>CustomerAuditController.GetActionTimeline</c>) on the
/// Admin host behind <c>Policy.CanViewAuditLog</c>: Employee and Customer roles are 403'd, an anonymous
/// caller is 401'd, an Administrator clears the gate, the validator refuses a call keyed by neither or
/// both of user / resource with <c>audit.timeline.filter_required</c>, and the tenant filter keeps
/// another operator's rows out of the page. The three-table merge itself is proven on real Postgres in
/// <c>Cleansia.IntegrationTests/Features/Auditing/GetActionTimelineTests</c>.
/// </summary>
public sealed class CustomerAuditTimelinePolicyTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string ByUser = "/api/CustomerAudit/timeline?userId=cust-a";
    private const string ByOrder = "/api/CustomerAudit/timeline?resourceType=Order&resourceId=order-1";

    [Fact]
    public async Task NonAdmin_Employee_is_403d_on_timeline()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "emp-1", "emp-1@hosttests.local", UserProfile.Employee);

        var resp = await AdminClient(token).GetAsync(ByUser);

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task NonAdmin_Customer_is_403d_on_timeline()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "cust-1", "cust-1@hosttests.local", UserProfile.Customer);

        var resp = await AdminClient(token).GetAsync(ByUser);

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task Anonymous_caller_is_401d_on_timeline()
    {
        var resp = await AdminHost.CreateClient().GetAsync(ByUser);

        HttpAssert.IsUnauthorized(resp);
    }

    [Theory]
    [InlineData("/api/CustomerAudit/timeline")]
    [InlineData("/api/CustomerAudit/timeline?userId=cust-a&resourceType=Order&resourceId=order-1")]
    [InlineData("/api/CustomerAudit/timeline?resourceType=Order")]
    public async Task Neither_or_both_keys_is_400_filter_required(string route)
    {
        var token = TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local", UserProfile.Administrator);

        var resp = await AdminClient(token).GetAsync(route);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(resp, BusinessErrorMessage.TimelineFilterRequired);
    }

    [Fact]
    public async Task Admin_clears_the_gate_by_user_and_by_resource_and_sees_only_their_own_tenants_rows()
    {
        await SeedAsync(ctx =>
        {
            ctx.CustomerActionAudits.AddRange(
                DomainSeed.CustomerAudit("caud-a1", "cust-a", HostTestTenants.A),
                DomainSeed.CustomerAudit("caud-b1", "cust-a", HostTestTenants.B));
            return Task.CompletedTask;
        });

        var token = TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local",
            UserProfile.Administrator, tenantId: HostTestTenants.A);

        var byUser = await AdminClient(token).GetAsync(ByUser);
        HttpAssert.IsOk(byUser);
        var userPage = await byUser.Content.ReadFromJsonAsync<PageResponse>();
        Assert.Equal(1, userPage!.Total);
        Assert.Equal("caud-a1", Assert.Single(userPage.Data).Id);

        var byOrder = await AdminClient(token).GetAsync(ByOrder);
        HttpAssert.IsOk(byOrder);
        var orderPage = await byOrder.Content.ReadFromJsonAsync<PageResponse>();
        Assert.Equal(1, orderPage!.Total);
        Assert.Equal("caud-a1", Assert.Single(orderPage.Data).Id);
    }

    private sealed record PageResponse(int Total, List<EntryResponse> Data);

    private sealed record EntryResponse(int Source, string Id, string Action);
}
