using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// The retry command. What may be retried is a rule of its own — a Failed deletion, or a Processing one
/// that no live run can still be on — and the handler records the actor as the session's e-mail or, for
/// the timer, <c>system</c>, and emits the same scope-and-subject-id-only snapshot the deletion does, keyed
/// on the request row the admin acted on.
/// </summary>
public sealed class AdminRetryUserDeletionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(GdprRequestStatus.Failed, 0, true)]
    [InlineData(GdprRequestStatus.Failed, 400, true)]
    [InlineData(GdprRequestStatus.Processing, 5, false)]
    [InlineData(GdprRequestStatus.Processing, 29, false)]
    [InlineData(GdprRequestStatus.Processing, 31, true)]
    [InlineData(GdprRequestStatus.Pending, 400, false)]
    [InlineData(GdprRequestStatus.Completed, 400, false)]
    public void Only_A_Failed_Or_Abandoned_Deletion_Is_Retryable(GdprRequestStatus status, int minutesAgo, bool expected)
    {
        var request = Request(GdprRequest.DeletionRequestType, status, Now.AddMinutes(-minutesAgo));

        Assert.Equal(expected, AdminRetryUserDeletion.IsRetryable(request, Now));
    }

    [Fact]
    public void A_Processing_Row_Is_Judged_By_Its_Last_Attempt_Not_Its_Filing()
    {
        var request = Request(GdprRequest.DeletionRequestType, GdprRequestStatus.Processing, Now.AddDays(-2));
        request.Updated("system", Now.AddMinutes(-5));

        Assert.False(AdminRetryUserDeletion.IsRetryable(request, Now));
    }

    [Fact]
    public void A_Failed_Export_Is_Not_A_Deletion_And_Is_Never_Retried_Here()
    {
        var request = Request(GdprAuditReasons.ExportRequestType, GdprRequestStatus.Failed, Now.AddDays(-1));

        Assert.False(AdminRetryUserDeletion.IsRetryable(request, Now));
    }

    [Fact]
    public async Task An_Admin_Retry_Records_The_Admin_As_Actor_And_Snapshots_Scope_And_Subject_Only()
    {
        var auditContext = new AuditContext();
        var service = new Mock<IGdprDeletionService>();
        (string ProcessedBy, string? Notes)? resolved = null;
        service.Setup(s => s.RetryDeletionAsync("request-1", It.IsAny<Func<User, (string, string?)>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Func<User, (string, string?)>, CancellationToken>((_, resolve, _) => resolved = resolve(Subject()))
            .ReturnsAsync(BusinessResult.Success());

        var handler = new AdminRetryUserDeletion.Handler(
            new TestUserSessionProvider("admin-1", "admin@cleansia.test"), service.Object, auditContext);
        var result = await handler.Handle(new AdminRetryUserDeletion.Command("request-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(("admin@cleansia.test", "Retried by admin@cleansia.test"), resolved);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("GdprRequest", snapshot!.ResourceType);
        Assert.Equal("request-1", snapshot.ResourceId);
        Assert.Contains("\"subjectUserId\":\"subject-1\"", snapshot.AfterJson);
        Assert.Contains("\"scope\":\"Deletion\"", snapshot.AfterJson);
        Assert.DoesNotContain("jana", snapshot.AfterJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", snapshot.AfterJson);
    }

    [Fact]
    public async Task The_Timer_Has_No_Session_And_Is_Recorded_As_System()
    {
        var service = new Mock<IGdprDeletionService>();
        (string ProcessedBy, string? Notes)? resolved = null;
        service.Setup(s => s.RetryDeletionAsync("request-1", It.IsAny<Func<User, (string, string?)>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Func<User, (string, string?)>, CancellationToken>((_, resolve, _) => resolved = resolve(Subject()))
            .ReturnsAsync(BusinessResult.Success());

        var handler = new AdminRetryUserDeletion.Handler(new TestUserSessionProvider([]), service.Object, new AuditContext());
        await handler.Handle(new AdminRetryUserDeletion.Command("request-1"), CancellationToken.None);

        Assert.Equal((GdprAuditReasons.SystemActor, "Retried by system"), resolved);
        Assert.Equal("system", GdprAuditReasons.SystemActor);
    }

    [Fact]
    public async Task A_Retry_That_Fails_Emits_No_Snapshot()
    {
        var auditContext = new AuditContext();
        var service = new Mock<IGdprDeletionService>();
        service.Setup(s => s.RetryDeletionAsync(It.IsAny<string>(), It.IsAny<Func<User, (string, string?)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure(new Error("userId", BusinessErrorMessage.GdprDeletionBlockedByOrder)));

        var handler = new AdminRetryUserDeletion.Handler(
            new TestUserSessionProvider("admin-1", "admin@cleansia.test"), service.Object, auditContext);
        var result = await handler.Handle(new AdminRetryUserDeletion.Command("request-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(auditContext.DrainSnapshot());
    }

    [Fact]
    public void The_Retry_Is_A_Sensitive_Admin_Action_Keyed_On_The_Request_Row()
    {
        var descriptor = AuditActionDescriptor.For(typeof(AdminRetryUserDeletion.Command));

        Assert.Equal("gdpr.user.delete.retry", descriptor.Action);
        Assert.Equal("GdprRequest", descriptor.ResourceType);
        Assert.True(descriptor.Sensitive);
        Assert.Equal("request-1", AuditResourceResolver.ResolveResourceId(new AdminRetryUserDeletion.Command("request-1"), descriptor.ResourceType));
    }

    private static User Subject()
    {
        var user = User.CreateWithPassword("jana.novakova@example.com", "Password-1", "Jana", "Novakova");
        user.Id = "subject-1";
        return user;
    }

    private static GdprRequest Request(string type, GdprRequestStatus status, DateTimeOffset createdOn)
    {
        var request = GdprRequest.Create("subject-1", type);
        request.Created("seed", createdOn);
        switch (status)
        {
            case GdprRequestStatus.Processing:
                request.MarkProcessing();
                break;
            case GdprRequestStatus.Completed:
                request.MarkCompleted("admin");
                break;
            case GdprRequestStatus.Failed:
                request.MarkFailed("admin", "boom");
                break;
        }

        return request;
    }
}
