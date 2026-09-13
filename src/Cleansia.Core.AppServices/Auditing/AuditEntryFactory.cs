using System.Diagnostics;
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
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
///
/// <para>The customer pair (ADR-0062 D1) builds the <c>CustomerActionAudit</c> row the same way, plus
/// the request context: <c>ClientAudience</c> is the host that served the request (never the JWT — it is
/// null on exactly the anonymous rows that most need it), IP/device come from
/// <see cref="IRequestMetadataProvider"/>, and the subject is the session's user id, falling back to the
/// snapshot's <c>ActorUserId</c> only when the session has none (S1: the session wins).</para>
/// </summary>
public sealed class AuditEntryFactory(
    IUserSessionProvider userSessionProvider,
    IRequestMetadataProvider requestMetadataProvider,
    IHostAudienceProvider hostAudienceProvider)
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

    public CustomerActionAudit CreateCustomerSuccess(object request, AuditActionDescriptor descriptor, AuditSnapshot? snapshot)
    {
        return BuildCustomer(request, descriptor, success: true, errorCode: null, snapshot);
    }

    public CustomerActionAudit CreateCustomerFailure(object request, AuditActionDescriptor descriptor, string? errorCode)
    {
        return BuildCustomer(request, descriptor, success: false, errorCode, snapshot: null);
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

    private CustomerActionAudit BuildCustomer(
        object request,
        AuditActionDescriptor descriptor,
        bool success,
        string? errorCode,
        AuditSnapshot? snapshot)
    {
        var sessionUserId = userSessionProvider.GetUserId();

        return CustomerActionAudit.Create(
            userId: string.IsNullOrWhiteSpace(sessionUserId) ? snapshot?.ActorUserId : sessionUserId,
            clientAudience: hostAudienceProvider.Audience,
            ipAddress: requestMetadataProvider.IpAddress,
            deviceLabel: requestMetadataProvider.DeviceLabel,
            deviceId: requestMetadataProvider.DeviceId,
            action: descriptor.Action,
            resourceType: snapshot?.ResourceType ?? descriptor.ResourceType,
            // A failure row's id is read off the request as the client sent it; clamping keeps a
            // malformed-id probe recorded instead of failing the out-of-band insert.
            resourceId: Clamp(
                snapshot?.ResourceId
                    ?? AuditResourceResolver.ResolveExact(request, descriptor.ResourceType, descriptor.ResourceIdProperty),
                CustomerActionAudit.ResourceIdMaxLength),
            success: success,
            errorCode: Clamp(errorCode, CustomerActionAudit.ErrorCodeMaxLength),
            payloadJson: snapshot?.AfterJson,
            correlationId: ResolveCorrelationId());
    }

    private static string? Clamp(string? value, int maxLength) =>
        value is { Length: var length } && length > maxLength ? value[..maxLength] : value;

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
