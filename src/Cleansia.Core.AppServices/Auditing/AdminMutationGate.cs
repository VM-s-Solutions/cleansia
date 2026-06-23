using System.Security.Claims;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// ADR-0012 D3 — the single host-independent admin-mutation discriminator, shared by the inner
/// <c>AuditLogBehavior</c> (success + handler-returned failure) and the outer
/// <c>AuditFailureCaptureBehavior</c> (validation reject + commit-throw). A request is an auditable admin
/// mutation iff its <c>[AuditAction]</c> descriptor is audited, the type name ends <c>Command</c>, and the
/// caller carries the Administrator role claim (precedent <c>AddDisputeMessage.cs:57</c>). Both behaviors
/// MUST agree exactly so the failure paths cover the same surface the success path does.
/// </summary>
public static class AdminMutationGate
{
    private const string CommandSuffix = "Command";

    public static bool IsAuditable(object request, AuditActionDescriptor descriptor, IUserSessionProvider userSessionProvider)
    {
        return descriptor.Audited
            && request.GetType().Name.EndsWith(CommandSuffix, StringComparison.Ordinal)
            && userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value == UserProfile.Administrator.ToString();
    }
}
