using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class LoyaltyAccountRepository(CleansiaDbContext context)
    : BaseRepository<LoyaltyAccount>(context), ILoyaltyAccountRepository
{
    public Task<LoyaltyAccount?> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
    }

    public async Task<LoyaltyAccount> EnsureForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var existing = await GetDbSet()
            .Include(a => a.Transactions)
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
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }
}
