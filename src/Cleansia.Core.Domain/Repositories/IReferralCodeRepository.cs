using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.Domain.Repositories;

public interface IReferralCodeRepository : IRepository<ReferralCode, string>
{
    /// <summary>
    /// Returns the user's lifetime referral code, or null if one hasn't been
    /// generated yet (lazy-create happens in <c>ReferralService.EnsureCodeForUserAsync</c>).
    /// </summary>
    Task<ReferralCode?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Lookup a code by its (canonical, uppercase) value. Tenant scoping is
    /// handled by the EF global query filter.
    /// </summary>
    Task<ReferralCode?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Cheap existence probe used by the collision-retry loop in
    /// <c>ReferralService</c> when generating a fresh code.
    /// </summary>
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);
}
