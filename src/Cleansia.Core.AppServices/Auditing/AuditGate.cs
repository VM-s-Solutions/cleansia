using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;

namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// The single discriminator shared by the inner <c>AuditLogBehavior</c> (success +
/// handler-returned failure) and the outer <c>AuditFailureCaptureBehavior</c> (validation reject +
/// commit-throw). Both behaviors MUST agree exactly so the failure paths cover the same surface the
/// success path does. Two arms:
/// <list type="bullet">
///   <item><b>Admin (ADR-0012 D3, opt-out):</b> the descriptor is audited, the type name ends
///   <c>Command</c>, and the caller carries the Administrator role claim (precedent
///   <c>AddDisputeMessage.cs:57</c>). Every admin mutation, marked or not.</item>
///   <item><b>Customer (ADR-0062 D1, opt-in):</b> the marker says <c>Audience = Customer</c> and the
///   caller is a Customer — or anonymous, but only where the marker says <c>AllowsAnonymousActor</c>
///   AND the serving host is a customer host. A system job has no role claim either, and a marker that
///   does not allow an anonymous actor keeps it from being recorded as a guest act.</item>
/// </list>
/// The admin arm wins: an Administrator running a customer-marked command lands in the admin table. An
/// Employee lands nowhere (<c>EmployeeActionAudit</c> is handler-written).
///
/// <para><b>Why the anonymous arm reads the host.</b> The session acts (<c>Login</c>, the password
/// reset, the e-mail confirmation) are anonymous by nature and several of their commands are routed on
/// the partner hosts too, where the caller is a cleaner whose session history belongs in no table: the
/// employee table is not for sessions and the customer table is not theirs. A Customer-role caller
/// needs no such check — a customer token is only ever valid on a customer host.</para>
/// </summary>
public static class AuditGate
{
    private const string CommandSuffix = "Command";

    public static AuditAudience? Resolve(
        object request,
        AuditActionDescriptor descriptor,
        IUserSessionProvider userSessionProvider,
        IHostAudienceProvider hostAudienceProvider)
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
            && (role == UserProfile.Customer.ToString()
                || (role is null && descriptor.AllowsAnonymousActor && IsCustomerHost(hostAudienceProvider))))
        {
            return AuditAudience.Customer;
        }

        return null;
    }

    private static bool IsCustomerHost(IHostAudienceProvider hostAudienceProvider) =>
        hostAudienceProvider.Audience == JwtAudiences.Customer;
}
