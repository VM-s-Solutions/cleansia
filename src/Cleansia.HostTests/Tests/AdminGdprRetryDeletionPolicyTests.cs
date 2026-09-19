using System.Net;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The failed-deletion retry (<c>AdminGdprController.RetryUserDeletion</c>) behind
/// <c>Policy.CanAdminDeleteUserAccount</c>, end-to-end against the real auth/authz pipeline: Employee and
/// Customer roles are 403'd at the gate, an anonymous caller is 401'd, an Administrator clears it and
/// the Failed row comes back Completed with the subject erased and a <c>gdpr.user.delete.retry</c> audit
/// row behind it; a Completed row is refused with <c>gdpr.request_not_retryable</c>. The request list's
/// new status filter answers only the rows in that status.
/// </summary>
public sealed class AdminGdprRetryDeletionPolicyTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string SubjectId = "gdpr-retry-subject";
    private const string SubjectEmail = "retry-subject@hosttests.local";
    private const string FailedRequestId = "01HZZHOSTRETRYFAILED00001";
    private const string CompletedRequestId = "01HZZHOSTRETRYDONE000001";

    private static string RetryRoute(string requestId) => $"/api/v1/AdminGdpr/requests/{requestId}/retry-deletion";

    private const string ListRoute = "/api/v1/AdminGdpr/requests";

    [Fact]
    public async Task NonAdmin_Employee_is_403d_on_the_retry()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "emp-1", "emp-1@hosttests.local", UserProfile.Employee);

        var resp = await AdminClient(token).PostAsync(RetryRoute(FailedRequestId), content: null);

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task NonAdmin_Customer_is_403d_on_the_retry()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "cust-1", "cust-1@hosttests.local", UserProfile.Customer);

        var resp = await AdminClient(token).PostAsync(RetryRoute(FailedRequestId), content: null);

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task Anonymous_caller_is_401d_on_the_retry()
    {
        var resp = await AdminHost.CreateClient().PostAsync(RetryRoute(FailedRequestId), content: null);

        HttpAssert.IsUnauthorized(resp);
    }

    [Fact]
    public async Task Admin_clears_the_gate_the_failed_row_completes_the_subject_is_erased_and_the_act_is_audited()
    {
        await SeedSubjectWithRequestsAsync();
        var client = AdminClient(TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local", UserProfile.Administrator));

        var resp = await client.PostAsync(RetryRoute(FailedRequestId), content: null);

        HttpAssert.IsOk(resp);

        var request = await QueryAsync(ctx => ctx.GdprRequests.IgnoreQueryFilters().SingleAsync(r => r.Id == FailedRequestId));
        Assert.Equal(GdprRequestStatus.Completed, request.Status);
        Assert.Equal("admin-a@hosttests.local", request.ProcessedBy);
        Assert.Equal("DbUpdateException: boom\nRetried by admin-a@hosttests.local", request.Notes);

        var subject = await QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId));
        Assert.StartsWith("deleted_", subject.Email);
        Assert.False(subject.IsActive);

        var audit = await QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().SingleAsync(a => a.Action == "gdpr.user.delete.retry"));
        Assert.Equal("GdprRequest", audit.ResourceType);
        Assert.Equal(FailedRequestId, audit.ResourceId);
        Assert.Equal("admin-a", audit.ActorId);
        Assert.True(audit.Success);
        Assert.Contains(SubjectId, audit.AfterJson);
        Assert.DoesNotContain(SubjectEmail, audit.AfterJson ?? string.Empty);
    }

    [Fact]
    public async Task A_completed_request_is_refused_as_not_retryable()
    {
        await SeedSubjectWithRequestsAsync();
        var client = AdminClient(TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local", UserProfile.Administrator));

        var resp = await client.PostAsync(RetryRoute(CompletedRequestId), content: null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(resp, BusinessErrorMessage.GdprRequestNotRetryable);
    }

    [Fact]
    public async Task An_unknown_request_is_refused_as_not_found()
    {
        var client = AdminClient(TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local", UserProfile.Administrator));

        var resp = await client.PostAsync(RetryRoute("01HZZHOSTRETRYNOPE0000001"), content: null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(resp, BusinessErrorMessage.GdprRequestNotFound);
    }

    [Fact]
    public async Task The_list_filtered_by_status_answers_only_that_status()
    {
        await SeedSubjectWithRequestsAsync();
        var client = AdminClient(TestJwtFactory.Mint(AdminAudience, "admin-a", "admin-a@hosttests.local", UserProfile.Administrator));

        var failedOnly = await client.GetAsync($"{ListRoute}?status={(int)GdprRequestStatus.Failed}");
        var all = await client.GetAsync(ListRoute);

        HttpAssert.IsOk(failedOnly);
        HttpAssert.IsOk(all);
        using var failedDoc = JsonDocument.Parse(await failedOnly.Content.ReadAsStringAsync());
        using var allDoc = JsonDocument.Parse(await all.Content.ReadAsStringAsync());
        Assert.Equal(1, failedDoc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(FailedRequestId, Assert.Single(failedDoc.RootElement.GetProperty("data").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal(2, allDoc.RootElement.GetProperty("total").GetInt32());
    }

    private Task SeedSubjectWithRequestsAsync() =>
        SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var subject = DomainSeed.Customer(SubjectEmail);
            subject.Id = SubjectId;
            ctx.Users.Add(subject);

            var failed = GdprRequest.Create(SubjectId, GdprRequest.DeletionRequestType);
            failed.Id = FailedRequestId;
            failed.MarkFailed(SubjectEmail, "DbUpdateException: boom");
            ctx.GdprRequests.Add(failed);

            var completed = GdprRequest.Create(SubjectId, GdprRequest.DeletionRequestType);
            completed.Id = CompletedRequestId;
            completed.MarkCompleted("admin-a@hosttests.local", "Admin deletion by admin-a@hosttests.local");
            ctx.GdprRequests.Add(completed);
        });
}
