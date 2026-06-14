using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class UserConsentRepository(CleansiaDbContext context) : BaseRepository<UserConsent>(context), IUserConsentRepository
{
    public Task<List<UserConsent>> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return UserConsentsQuery(userId).ToListAsync(cancellationToken);
    }

    public Task<List<UserConsent>> GetByUserIdNoTrackingAsync(string userId, CancellationToken cancellationToken)
    {
        return UserConsentsQuery(userId).AsNoTracking().ToListAsync(cancellationToken);
    }

    private IQueryable<UserConsent> UserConsentsQuery(string userId)
    {
        return GetDbSet()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.ConsentType);
    }

    public Task<UserConsent?> GetByUserAndTypeAsync(string userId, ConsentType consentType, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ConsentType == consentType, cancellationToken);
    }
}
