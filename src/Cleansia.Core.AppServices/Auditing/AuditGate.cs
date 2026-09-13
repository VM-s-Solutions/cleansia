using System.Security.Claims;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// The single host-independent discriminator shared by the inner <c>AuditLogBehavior</c> (success +
/// handler-returned failure) and the outer <c>AuditFailureCaptureBehavior</c> (validation reject +
/// commit-throw). Both behaviors MUST agree exactly so the failure paths cover the same surface the
/// success path does. Two arms:
/// <list type="bullet">
///   <item><b>Admin (ADR-0012 D3, opt-out):</b> the descriptor is audited, the type name ends
///   <c>Command</c>, and the caller carries the Administrator role claim (precedent
///   <c>AddDisputeMessage.cs:57</c>). Every admin mutation, marked or not.</item>
///   <item><b>Customer (ADR-0062 D1, opt-in):</b> the marker says <c>Audience = Customer</c> and the
///   caller is a Customer — or anonymous, but only where the marker says <c>AllowsAnonymousActor</c>.
///   A system job has no role claim either, and a marker that does not allow an anonymous actor keeps
///   it from being recorded as a guest act.</item>
/// </list>
/// The admin arm wins: an Administrator running a customer-marked command lands in the admin table. An
/// Employee lands nowhere (<c>EmployeeActionAudit</c> is handler-written).
/// </summary>
public static class AuditGate
{
    private const string CommandSuffix = "Command";

    public static AuditAudience? Resolve(object request, AuditActionDescriptor descriptor, IUserSessionProvider userSessionProvider)
    {
        if (!descriptor.Audited || !request.GetType().Name.EndsWith(CommandSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;

        if (role == UserProfile.Administrator.ToString())
        {
            return AuditAudience.Admin;
        }

        if (descriptor.Audience == AuditAudience.Customer
            && (role == UserProfile.Customer.ToString() || (role is null && descriptor.AllowsAnonymousActor)))
        {
            return AuditAudience.Customer;
        }

        return null;
    }
}
