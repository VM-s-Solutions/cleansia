using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class LoyaltyAccountRepository(CleansiaDbContext context)
    : BaseRepository<LoyaltyAccount>(context), ILoyaltyAccountRepository
{
    // No Include(Transactions): the grant/revoke domain methods only APPEND to the ledger (they never
    // read it — denormalized LifetimePoints/CurrentTier drive the recompute), and no read caller reads
    // account.Transactions (activity lists fetch via LoyaltyTransactionRepository). EF tracks an
    // appended child without the collection pre-loaded, so loading the whole append-only ledger on the
    // booking/grant/revoke paths was pure over-fetch.
    public Task<LoyaltyAccount?> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
    }

    public Task<LoyaltyAccount?> GetByUserIdTierOnlyAsync(string userId, CancellationToken cancellationToken)
    {
        // Booking/quote hot path (LoyaltyService.ResolveTierDiscountForOrderAsync) reads only
        // CurrentTier. No-tracking + ledger-free keeps the per-quote round-trip minimal.
        return GetDbSet()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
    }

    public async Task<LoyaltyAccount> EnsureForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var existing = await GetDbSet()
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var account = LoyaltyAccount.Create(userId);
        Add(account);
        return account;
    }

    public override Task<LoyaltyAccount?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }
}
