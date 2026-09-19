using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0062 D6 — the three-source timeline (<c>CustomerAuditController.GetActionTimeline</c>) on the
/// Admin host behind <c>Policy.CanViewAuditLog</c>: Employee and Customer roles are 403'd, an anonymous
/// caller is 401'd, an Administrator clears the gate, the validator refuses a call keyed by neither or
/// both of user / resource with <c>audit.timeline.filter_required</c>, a page past the shared
/// <c>DataRangeRequest</c> bounds or the timeline's own limit is 400, and the tenant filter keeps
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
    public async Task An_offset_past_the_shared_DataRangeRequest_bound_is_400()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local", UserProfile.Administrator);

        var resp = await AdminClient(token).GetAsync(ByUser + "&offset=501");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task A_limit_past_the_timelines_own_cap_is_400_page_size_exceeded()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local", UserProfile.Administrator);

        var resp = await AdminClient(token).GetAsync(ByUser + "&limit=101");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(resp, BusinessErrorMessage.PageSizeExceeded);
    }

    [Fact]
    public async Task Account_admin_sees_user_history_across_operators_but_resource_history_stays_filtered()
    {
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var customer = DomainSeed.Customer("cust-a@hosttests.local", HostTestTenants.A);
            customer.Id = "cust-a";
            ctx.Users.Add(customer);
            ctx.CustomerActionAudits.AddRange(
                DomainSeed.CustomerAudit("caud-a1", "cust-a", HostTestTenants.A),
                DomainSeed.CustomerAudit("caud-b1", "cust-a", HostTestTenants.B));
        });

        var token = TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local",
            UserProfile.Administrator, tenantId: HostTestTenants.A);

        var byUser = await AdminClient(token).GetAsync(ByUser);
        HttpAssert.IsOk(byUser);
        var userPage = await byUser.Content.ReadFromJsonAsync<PageResponse>();
        Assert.Equal(2, userPage!.Total);
        Assert.Equal(new[] { "caud-a1", "caud-b1" }, userPage.Data.Select(e => e.Id).OrderBy(id => id));

        var byOrder = await AdminClient(token).GetAsync(ByOrder);
        HttpAssert.IsOk(byOrder);
        var orderPage = await byOrder.Content.ReadFromJsonAsync<PageResponse>();
        Assert.Equal(1, orderPage!.Total);
        Assert.Equal("caud-a1", Assert.Single(orderPage.Data).Id);

        var paged = await AdminClient(token).GetAsync("/api/CustomerAudit/get-paged?filter.userId=cust-a");
        HttpAssert.IsOk(paged);
        var auditPage = await paged.Content.ReadFromJsonAsync<PageResponse>();
        Assert.Equal(2, auditPage!.Total);
        Assert.Equal(new[] { "caud-a1", "caud-b1" }, auditPage.Data.Select(e => e.Id).OrderBy(id => id));

        var otherAdmin = TestJwtFactory.Mint(AdminAudience, "admin-b", "admin-b@hosttests.local",
            UserProfile.Administrator, tenantId: HostTestTenants.B);
        var foreignUser = await AdminClient(otherAdmin).GetAsync(ByUser);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, foreignUser.StatusCode);
        var foreignPaged = await AdminClient(otherAdmin).GetAsync("/api/CustomerAudit/get-paged?filter.userId=cust-a");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, foreignPaged.StatusCode);
    }

    private sealed record PageResponse(int Total, List<EntryResponse> Data);

    private sealed record EntryResponse(int Source, string Id, string Action);
}
