using System.Diagnostics;
using System.Security.Claims;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// ADR-0012 D2.1/D5/D5.1 — assembles the append-only <c>AdminActionAudit</c> row from the actor session,
/// the resolved action descriptor, the optional drained snapshot, and the ambient correlation id. Shared
/// by the success path (the behavior) and the failure path (the out-of-band sink) so both shapes agree
/// on actor/action/resource/correlation. Holds no domain math — the before/after, if any, comes from the
/// handler's pre-redacted snapshot.
/// </summary>
public sealed class AuditEntryFactory(IUserSessionProvider userSessionProvider)
{
    private const string SystemActor = "System";

    public AdminActionAudit CreateSuccess(object request, AuditActionDescriptor descriptor, AuditSnapshot? snapshot)
    {
        return Build(request, descriptor, success: true, errorCode: null, snapshot);
    }

    public AdminActionAudit CreateFailure(object request, AuditActionDescriptor descriptor, string? errorCode)
    {
        return Build(request, descriptor, success: false, errorCode, snapshot: null);
    }

    private AdminActionAudit Build(
        object request,
        AuditActionDescriptor descriptor,
        bool success,
        string? errorCode,
        AuditSnapshot? snapshot)
    {
        var actorId = userSessionProvider.GetUserId();

        return new AdminActionAudit
        {
            ActorId = string.IsNullOrWhiteSpace(actorId) ? SystemActor : actorId,
            ActorEmail = userSessionProvider.GetUserEmail(),
            ActorProfile = ResolveActorProfile(),
            Action = descriptor.Action,
            ResourceType = snapshot?.ResourceType ?? descriptor.ResourceType,
            ResourceId = snapshot?.ResourceId ?? AuditResourceResolver.ResolveResourceId(request, descriptor.ResourceType),
            Success = success,
            ErrorCode = errorCode,
            OccurredOn = DateTimeOffset.UtcNow,
            Reason = snapshot?.Reason,
            BeforeJson = snapshot?.BeforeJson,
            AfterJson = snapshot?.AfterJson,
            CorrelationId = ResolveCorrelationId()
        };
    }

    private UserProfile ResolveActorProfile()
    {
        return Enum.TryParse<UserProfile>(
            userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value, out var profile)
            ? profile
            : UserProfile.Administrator;
    }

    private static string? ResolveCorrelationId()
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return null;
        }

        return activity.TraceId == default ? activity.Id : activity.TraceId.ToString();
    }
}
