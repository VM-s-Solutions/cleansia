using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class UserConsentRepository(CleansiaDbContext context) : BaseRepository<UserConsent>(context), IUserConsentRepository
{
    public Task<List<UserConsent>> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.ConsentType)
            .ToListAsync(cancellationToken);
    }

    public Task<UserConsent?> GetByUserAndTypeAsync(string userId, ConsentType consentType, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ConsentType == consentType, cancellationToken);
    }
}
