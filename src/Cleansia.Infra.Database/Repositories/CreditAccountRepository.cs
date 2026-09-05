using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class CreditAccountRepository(CleansiaDbContext context)
    : BaseRepository<CreditAccount>(context), ICreditAccountRepository
{
    public Task<CreditAccount?> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
    }

    public async Task<bool> TryDebitAsync(
        string creditAccountId, decimal amount, CancellationToken cancellationToken)
    {
        if (amount <= 0m)
        {
            return false;
        }

        // A CONDITIONAL UPDATE is the arbiter, and nothing else can be.
        //
        // A balance is an aggregate: two concurrent spends of 500 against a balance of 500 have no
        // shared key to collide on, so no unique index can refuse the second one. Read-then-subtract
        // in the app layer loses the race by construction. This is one SQL statement — Postgres
        // evaluates `Balance >= amount` and applies the decrement under the same row lock — so the
        // second spend matches zero rows and gets false.
        //
        // It also refuses rather than clamping. LoyaltyAccount.RevokePoints does
        // Math.Max(0, points - n), which on a tier counter is harmless and on money is a silent
        // partial-spend: the customer would get the full discount and the ledger would floor at zero.
        //
        // DELIBERATE EXCEPTION to "never commit outside the UnitOfWork pipeline", exactly as
        // PromoCodeRepository.TryIncrementGlobalRedemptionsAsync documents: ExecuteUpdateAsync issues
        // SQL immediately and is not change-tracked. That independence is REQUIRED — the funds must
        // be taken (or refused) on their own, not at the end of whatever transaction the caller is in.
        // The caller compensates by issuing the amount back if its own work then fails.
        var rowsAffected = await GetQueryable()
            .Where(a => a.Id == creditAccountId && a.Balance >= amount)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.Balance, a => a.Balance - amount),
                cancellationToken);

        return rowsAffected > 0;
    }
}
