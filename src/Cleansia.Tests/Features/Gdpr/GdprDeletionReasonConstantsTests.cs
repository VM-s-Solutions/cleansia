using System.Reflection;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// IA-19 — the GDPR deactivation reason / fallback actor literals were replaced by
/// <see cref="GdprAuditReasons"/> constants. Pins the persisted reason strings byte-identical so the
/// extraction is behavior-preserving: self-delete forwards "GDPR_DELETION", admin-delete forwards
/// "GDPR_ADMIN_DELETION", and a missing admin email falls back to actor "admin". The handlers are
/// internal, so they're invoked through reflection (mirrors the device handler tests).
/// </summary>
public class GdprDeletionReasonConstantsTests
{
    private const string UserId = "user-1";

    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IGdprDeletionService> _gdprService = new();

    private string _capturedReason = string.Empty;
    private string _capturedProcessedBy = string.Empty;
    private string? _capturedNotes;

    public GdprDeletionReasonConstantsTests()
    {
        var probe = User.CreateWithPassword("data-subject@example.com", "Password1", "Data", "Subject");
        probe.Id = UserId;
        _gdprService
            .Setup(s => s.DeleteUserAccountAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<User, (string ProcessedBy, string? Notes)>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, Func<User, (string ProcessedBy, string? Notes)>, CancellationToken>(
                (_, reason, resolveActor, _2) =>
                {
                    _capturedReason = reason;
                    (_capturedProcessedBy, _capturedNotes) = resolveActor(probe);
                })
            .ReturnsAsync(BusinessResult.Success());
    }

    private async Task<BusinessResult> InvokeHandler(Type featureType, object command)
    {
        var handlerType = featureType.GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, _session.Object, _gdprService.Object)!;
        var handleMethod = handlerType!.GetMethod("Handle");
        Assert.NotNull(handleMethod);
        var task = (Task<BusinessResult>)handleMethod!.Invoke(handler, [command, CancellationToken.None])!;
        return await task;
    }

    [Fact]
    public async Task SelfDelete_ForwardsReason_GdprDeletion_ByteIdentical()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        var result = await InvokeHandler(typeof(DeleteUserAccount), new DeleteUserAccount.Command());

        Assert.True(result.IsSuccess);
        Assert.Equal("GDPR_DELETION", _capturedReason);
        Assert.Equal(GdprAuditReasons.SelfDeletion, _capturedReason);
    }

    [Fact]
    public async Task AdminDelete_ForwardsReason_GdprAdminDeletion_ByteIdentical()
    {
        _session.Setup(s => s.GetUserEmail()).Returns("admin@cleansia.test");

        var result = await InvokeHandler(typeof(AdminDeleteUserAccount), new AdminDeleteUserAccount.Command(UserId));

        Assert.True(result.IsSuccess);
        Assert.Equal("GDPR_ADMIN_DELETION", _capturedReason);
        Assert.Equal(GdprAuditReasons.AdminDeletion, _capturedReason);
        Assert.Equal("admin@cleansia.test", _capturedProcessedBy);
    }

    [Fact]
    public async Task AdminDelete_MissingEmail_FallsBackToActor_Admin_ByteIdentical()
    {
        _session.Setup(s => s.GetUserEmail()).Returns((string?)null);

        var result = await InvokeHandler(typeof(AdminDeleteUserAccount), new AdminDeleteUserAccount.Command(UserId));

        Assert.True(result.IsSuccess);
        Assert.Equal("admin", _capturedProcessedBy);
        Assert.Equal(GdprAuditReasons.FallbackAdminActor, _capturedProcessedBy);
    }
}
