using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;

namespace Cleansia.Core.AppServices.Tenancy;

/// <summary>
/// Cleaners of a deactivated company are refused on the partner audiences; its administrators are not,
/// because they are the hands that settle it and no holding role exists yet (ADR-0064 O-1), and a
/// cleaner signing in as a customer keeps the customer host as the surviving GDPR channel. The profile
/// is checked before the registry is read, so a customer sign-in never pays the <c>Tenants</c> read.
/// </summary>
public sealed class CompanySignInGate(ITenantRepository tenantRepository) : ICompanySignInGate
{
    public static readonly IReadOnlySet<UserProfile> RefusedProfiles = new HashSet<UserProfile> { UserProfile.Employee };

    private static readonly IReadOnlySet<string> GatedAudiences = new HashSet<string>(StringComparer.Ordinal)
    {
        JwtAudiences.Partner,
        JwtAudiences.Mobile,
    };

    private readonly Dictionary<string, bool> _deactivatedByTenant = new(StringComparer.Ordinal);

    public async Task<string?> RefusalForAsync(User user, string audience, CancellationToken cancellationToken)
    {
        if (!RefusedProfiles.Contains(user.Profile) || !GatedAudiences.Contains(audience) || string.IsNullOrEmpty(user.TenantId))
        {
            return null;
        }

        if (!_deactivatedByTenant.TryGetValue(user.TenantId, out var deactivated))
        {
            var tenant = await tenantRepository.GetByIdAsync(user.TenantId, cancellationToken);
            deactivated = tenant?.IsDeactivated ?? false;
            _deactivatedByTenant[user.TenantId] = deactivated;
        }

        return deactivated ? BusinessErrorMessage.CompanyDeactivated : null;
    }
}
