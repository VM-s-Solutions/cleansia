using System.Net;
using System.Net.Http.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0062 D6-export — the admin subject export (<c>AdminGdprController.ExportUserData</c>) is a POST
/// now that it is an audited Command, behind <c>Policy.CanAdminExportUserData</c>, end-to-end against
/// the real auth/authz pipeline: Employee and Customer roles are 403'd at the gate, an anonymous caller
/// is 401'd, an Administrator clears it and the response carries the subject's <c>customerActions</c>,
/// with the <c>GdprRequest</c> and the <c>gdpr.user.export</c> row committed behind it. The old GET is
/// gone: a client still calling it gets 405, not a silent unrecorded dump.
/// </summary>
public sealed class AdminGdprExportPolicyTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string SubjectId = "gdpr-export-subject";
    private const string SubjectEmail = "export-subject@hosttests.local";

    private static string Route(string userId) => $"/api/v1/AdminGdpr/export/{userId}";

    [Fact]
    public async Task NonAdmin_Employee_is_403d_on_the_admin_export()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "emp-1", "emp-1@hosttests.local", UserProfile.Employee);

        var resp = await AdminClient(token).PostAsync(Route(SubjectId), content: null);

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task NonAdmin_Customer_is_403d_on_the_admin_export()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "cust-1", "cust-1@hosttests.local", UserProfile.Customer);

        var resp = await AdminClient(token).PostAsync(Route(SubjectId), content: null);

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task Anonymous_caller_is_401d_on_the_admin_export()
    {
        var resp = await AdminHost.CreateClient().PostAsync(Route(SubjectId), content: null);

        HttpAssert.IsUnauthorized(resp);
    }

    [Fact]
    public async Task Admin_clears_the_gate_the_export_carries_the_trail_and_leaves_its_record_and_the_old_GET_is_gone()
    {
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var subject = DomainSeed.Customer(SubjectEmail);
            subject.Id = SubjectId;
            ctx.Users.Add(subject);
            ctx.CustomerActionAudits.Add(DomainSeed.CustomerAudit("caud-export-1", SubjectId, HostTestTenants.Default));
        });
        var token = TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local", UserProfile.Administrator);
        var client = AdminClient(token);

        var old = await client.GetAsync(Route(SubjectId));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, old.StatusCode);

        var resp = await client.PostAsync(Route(SubjectId), content: null);

        HttpAssert.IsOk(resp);
        var export = await resp.Content.ReadFromJsonAsync<ExportResponse>();
        Assert.NotNull(export);
        Assert.Equal(SubjectId, export!.Profile.Id);
        var action = Assert.Single(export.CustomerActions);
        Assert.Equal("customer.order.cancel", action.Action);
        Assert.Equal("203.0.113.9", action.IpAddress);
        Assert.Contains("feeRate", action.PayloadJson);

        var request = await QueryAsync(ctx => ctx.GdprRequests.IgnoreQueryFilters().SingleAsync(r => r.UserId == SubjectId));
        Assert.Equal("Export", request.RequestType);
        Assert.Equal(GdprRequestStatus.Completed, request.Status);
        Assert.Equal("admin-a@hosttests.local", request.ProcessedBy);

        var audit = await QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().SingleAsync(a => a.Action == "gdpr.user.export"));
        Assert.Equal(SubjectId, audit.ResourceId);
        Assert.Equal("admin-a", audit.ActorId);
        Assert.True(audit.Success);
    }

    private sealed record ExportResponse(ProfileResponse Profile, List<ActionResponse> CustomerActions);

    private sealed record ProfileResponse(string Id);

    private sealed record ActionResponse(string Action, string? IpAddress, string? PayloadJson);
}
