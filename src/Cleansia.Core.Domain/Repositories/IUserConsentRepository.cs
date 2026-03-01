using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IUserConsentRepository : IRepository<UserConsent, string>
{
    Task<List<UserConsent>> GetByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task<UserConsent?> GetByUserAndTypeAsync(string userId, ConsentType consentType, CancellationToken cancellationToken);
}
