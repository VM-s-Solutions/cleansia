using System.Diagnostics;
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// ADR-0012 D2.1/D5/D5.1 — assembles the append-only <c>AdminActionAudit</c> row from the actor session,
/// the resolved action descriptor, the optional drained snapshot, and the ambient correlation id. Shared
/// by the success path (the behavior) and the failure path (the out-of-band sink) so both shapes agree
/// on actor/action/resource/correlation. Holds no domain math — the before/after, if any, comes from the
/// handler's pre-redacted snapshot. The label is the descriptor's admin one: a customer-marked command an
/// Administrator runs is recorded as the admin act it is.
///
/// <para>Both rows name their subject the same way: the session's user id, falling back to the
/// snapshot's <c>ActorUserId</c> only when the session has none (S1: the session wins) — the admin
/// sign-in runs anonymously like the customer one, and its row is keyed on the account the handler or
/// the validator named, not on <c>System</c>. The admin row's profile follows the same rule: the
/// session's role, else the profile named with the account, and <c>Administrator</c> only for the row
/// that names nobody (System) — a customer refused the admin host is a Customer's row, since the
/// customer's own incident file collects it. A failure row reads the subject and the resource off the
/// snapshot too — a validator names the account it is about to refuse through the same seam — but never
/// its payload, before/after or reason: on a refusal the only payload there could be is the request's
/// own words.</para>
/// <para>Secret-key guest cancellation always has a null customer actor, even when an unrelated
/// account session or snapshot is present; the secret proves access to the booking, not an account.</para>
///
/// <para>The customer pair (ADR-0062 D1) builds the <c>CustomerActionAudit</c> row the same way, plus
/// the request context: <c>ClientAudience</c> is the host that served the request (never the JWT — it is
/// null on exactly the anonymous rows that most need it), IP and device label come from
/// <see cref="IRequestMetadataProvider"/>, and the device id is the session's signed <c>device_id</c>
/// claim (the header only where there is no session to bind one).</para>
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

    public AdminActionAudit CreateFailure(object request, AuditActionDescriptor descriptor, string? errorCode, AuditSnapshot? snapshot = null)
    {
        return Build(request, descriptor, success: false, errorCode, snapshot);
    }

    public CustomerActionAudit CreateCustomerSuccess(object request, AuditActionDescriptor descriptor, AuditSnapshot? snapshot)
    {
        return BuildCustomer(request, descriptor, success: true, errorCode: null, snapshot);
    }

    public CustomerActionAudit CreateCustomerFailure(object request, AuditActionDescriptor descriptor, string? errorCode, AuditSnapshot? snapshot = null)
    {
        return BuildCustomer(request, descriptor, success: false, errorCode, snapshot);
    }

    private AdminActionAudit Build(
        object request,
        AuditActionDescriptor descriptor,
        bool success,
        string? errorCode,
        AuditSnapshot? snapshot)
    {
        var sessionUserId = userSessionProvider.GetUserId();
        var actorId = string.IsNullOrWhiteSpace(sessionUserId) ? snapshot?.ActorUserId : sessionUserId;

        return new AdminActionAudit
        {
            ActorId = string.IsNullOrWhiteSpace(actorId) ? SystemActor : actorId,
            ActorEmail = userSessionProvider.GetUserEmail(),
            ActorProfile = ResolveActorProfile(snapshot),
            ActorAdminRole = ResolveActorAdminRole(),
            Action = descriptor.AdminAction,
            ResourceType = snapshot?.ResourceType ?? descriptor.ResourceType,
            ResourceId = snapshot?.ResourceId ?? AuditResourceResolver.ResolveResourceId(request, descriptor.ResourceType),
            Success = success,
            ErrorCode = errorCode,
            OccurredOn = DateTimeOffset.UtcNow,
            Reason = success ? snapshot?.Reason : null,
            BeforeJson = success ? snapshot?.BeforeJson : null,
            AfterJson = success ? snapshot?.AfterJson : null,
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
        var actorId = request is IGuestOrderScopedRequest
            ? null
            : string.IsNullOrWhiteSpace(sessionUserId) ? snapshot?.ActorUserId : sessionUserId;

        return CustomerActionAudit.Create(
            userId: actorId,
            clientAudience: hostAudienceProvider.Audience,
            ipAddress: requestMetadataProvider.IpAddress,
            deviceLabel: requestMetadataProvider.DeviceLabel,
            // The X-Device-Id header is the client's word alone, re-sendable per request; the claim is the
            // device the token was minted for, the one device revocation acts on. A signed-in row therefore
            // records the claim or nothing, and only an anonymous act falls back to the header.
            deviceId: string.IsNullOrWhiteSpace(sessionUserId)
                ? requestMetadataProvider.DeviceId
                : userSessionProvider.GetTypedUserClaim(AuthExtensions.DeviceIdClaimType)?.Value,
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
            payloadJson: success ? snapshot?.AfterJson : null,
            correlationId: ResolveCorrelationId());
    }

    private static string? Clamp(string? value, int maxLength) =>
        value is { Length: var length } && length > maxLength ? value[..maxLength] : value;

    private UserProfile ResolveActorProfile(AuditSnapshot? snapshot)
    {
        return Enum.TryParse<UserProfile>(
            userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value, out var profile)
            ? profile
            : snapshot?.ActorProfile ?? UserProfile.Administrator;
    }

    private AdminRole? ResolveActorAdminRole()
    {
        return Enum.TryParse<AdminRole>(
            userSessionProvider.GetTypedUserClaim(AdminRoleSets.ClaimType)?.Value, out var role)
            && Enum.IsDefined(role)
            ? role
            : null;
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
