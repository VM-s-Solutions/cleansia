using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IUserConsentRepository : IRepository<UserConsent, string>
{
    Task<List<UserConsent>> GetByUserIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// No-tracking variant for read-only consent surfaces (admin/customer consent views, GDPR export).
    /// The tracked <see cref="GetByUserIdAsync"/> stays for the deletion path, which loads-then-withdraws.
    /// </summary>
    Task<List<UserConsent>> GetByUserIdNoTrackingAsync(string userId, CancellationToken cancellationToken);

    Task<UserConsent?> GetByUserAndTypeAsync(string userId, ConsentType consentType, CancellationToken cancellationToken);
}
