using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class SavedCardRepository(CleansiaDbContext context)
    : BaseRepository<SavedCard>(context), ISavedCardRepository
{
    public async Task<IReadOnlyList<SavedCard>> GetCapturedForUserAsync(string userId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(c => c.Currency)
            .Where(c => c.UserId == userId && c.IsActive && c.CapturedOn != null)
            .OrderByDescending(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SavedCard>> GetCapturedForUserInCurrencyAsync(
        string userId, string currencyId, CancellationToken cancellationToken)
    {
        // The owner is pinned by a JWT or an authorized persisted order; their account can belong to
        // another operator than the booking or the cleaner recording its cash.
        return await GetDbSet()
            .IgnoreQueryFilters()
            .Where(c => c.UserId == userId && c.CurrencyId == currencyId && c.IsActive && c.CapturedOn != null)
            .ToListAsync(cancellationToken);
    }

    public Task<SavedCard?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task RemoveForUserIgnoringTenantAsync(string userId, CancellationToken cancellationToken)
    {
        var rows = await GetDbSet().IgnoreQueryFilters().Where(c => c.UserId == userId).ToListAsync(cancellationToken);
        GetDbSet().RemoveRange(rows);
    }
}
