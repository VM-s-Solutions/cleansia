using System.Net;
using System.Net.Http.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0062 D5-export / Q-AUD-O3 — the customer's own export (<c>GdprController.ExportMyData</c> on the
/// Customer host) moved to POST when it became a Command, so the <c>GdprRequest("Export")</c> row it
/// had always added finally commits. End-to-end: the caller's trail is in the body, the request row is
/// in the database, no admin row is written for a self-export, and the old GET answers 405.
/// </summary>
public sealed class GdprSelfExportRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string Route = "/api/v1/Gdpr/export";
    private const string SubjectId = "gdpr-self-export-subject";
    private const string SubjectEmail = "self-export@hosttests.local";

    [Fact]
    public async Task Anonymous_caller_is_401d_on_the_self_export()
    {
        var resp = await CustomerClientAnonymous().PostAsync(Route, content: null);

        HttpAssert.IsUnauthorized(resp);
    }

    [Fact]
    public async Task The_caller_gets_their_own_trail_the_request_row_commits_without_an_admin_row_and_the_old_GET_is_gone()
    {
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var subject = DomainSeed.Customer(SubjectEmail);
            subject.Id = SubjectId;
            ctx.Users.Add(subject);
            ctx.CustomerActionAudits.AddRange(
                DomainSeed.CustomerAudit("caud-self-1", SubjectId, HostTestTenants.Default),
                DomainSeed.CustomerAudit("caud-other-1", "someone-else", HostTestTenants.Default));
        });
        var token = TestJwtFactory.Mint(CustomerAudience, SubjectId, SubjectEmail, UserProfile.Customer);
        var client = CustomerClient(token);

        var old = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, old.StatusCode);

        var resp = await client.PostAsync(Route, content: null);

        HttpAssert.IsOk(resp);
        var export = await resp.Content.ReadFromJsonAsync<ExportResponse>();
        Assert.NotNull(export);
        Assert.Equal(SubjectId, export!.Profile.Id);
        var action = Assert.Single(export.CustomerActions);
        Assert.Equal("customer.order.cancel", action.Action);

        var request = await QueryAsync(ctx => ctx.GdprRequests.IgnoreQueryFilters().SingleAsync(r => r.UserId == SubjectId));
        Assert.Equal("Export", request.RequestType);
        Assert.Equal(GdprRequestStatus.Completed, request.Status);

        Assert.Equal(0, await QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().CountAsync()));
    }

    private sealed record ExportResponse(ProfileResponse Profile, List<ActionResponse> CustomerActions);

    private sealed record ProfileResponse(string Id);

    private sealed record ActionResponse(string Action);
}
