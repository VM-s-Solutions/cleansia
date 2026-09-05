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

    public async Task<CreditAccount> EnsureForUserAsync(
        string userId, string currencyId, CancellationToken cancellationToken)
    {
        // No Include(Transactions). Issue() only APPENDS, and EF tracks an appended child without the
        // collection pre-loaded - same reasoning as LoyaltyAccountRepository, and it matters more here
        // because a long-lived customer's ledger is unbounded.
        var existing = await GetDbSet()
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var account = CreditAccount.Create(userId, currencyId, userId);
        Add(account);
        return account;
    }

    public async Task<CreditSpendable?> GetSpendableAsync(
        string userId, CancellationToken cancellationToken)
    {
        var row = await GetDbSet()
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => new CreditSpendable(a.Id, a.Balance, a.CurrencyId))
            .FirstOrDefaultAsync(cancellationToken);

        return row;
    }

    public async Task<bool> TryDebitAsync(
        string creditAccountId,
        decimal amount,
        CreditTransactionReason reason,
        string idempotencyKey,
        string actorId,
        CancellationToken cancellationToken,
        string? orderId = null,
        string? note = null)
    {
        if (amount <= 0m || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return false;
        }

        // ONE STATEMENT, so the balance and the ledger cannot disagree.
        //
        // The first draft of this method decremented the balance and left the caller to record the
        // movement. That is a footgun with money in it: the decrement auto-commits on its own, so a
        // caller whose own transaction then rolled back would have taken the funds and written no
        // ledger row, and nothing would ever notice — Balance == SUM(Transactions.Amount) is the one
        // invariant this design has, and it would be quietly false.
        //
        // A CTE rather than two statements in a transaction, deliberately. Postgres evaluates
        // `Balance >= amount` and applies the decrement under one row lock, and the INSERT can only
        // see rows the UPDATE actually returned — so insufficient funds writes nothing at all. It
        // also needs no transaction management of its own, which matters because this is called from
        // inside the UnitOfWork pipeline's transaction and opening a second one there would throw.
        //
        // A balance is an AGGREGATE: two concurrent spends share no key, so no unique index can
        // arbitrate them and a read-then-subtract loses the race by construction. The conditional
        // UPDATE is the arbiter. It also REFUSES rather than clamping — LoyaltyAccount.RevokePoints
        // does Math.Max(0, points - n), which is harmless on a tier counter and a silent
        // partial-spend on money.
        //
        // The idempotency key is the second guard: CreditTransactions has a plain unique index on it,
        // so a replayed spend raises 23505 and the whole statement — decrement included — rolls back.
        var rowsAffected = await context.Database.ExecuteSqlAsync(
            $"""
            WITH debited AS (
                UPDATE "CreditAccounts"
                SET "Balance" = "Balance" - {amount},
                    "UpdatedBy" = {actorId},
                    "UpdatedOn" = NOW()
                WHERE "Id" = {creditAccountId} AND "Balance" >= {amount}
                RETURNING "Id"
            )
            INSERT INTO "CreditTransactions" (
                "Id", "CreditAccountId", "Amount", "Reason", "OrderId", "DisputeId",
                "IdempotencyKey", "Note", "IsActive", "TenantId", "CreatedBy", "CreatedOn")
            SELECT
                {NewId()}, debited."Id", {-amount}, {(int)reason}, {orderId}, NULL,
                {idempotencyKey}, {note}, TRUE, NULL, {actorId}, NOW()
            FROM debited
            """,
            cancellationToken);

        return rowsAffected > 0;
    }

    /// <summary>
    /// A ULID for the ledger row. The raw statement bypasses EF's key generation, so the id is
    /// produced the same way <see cref="Core.Domain.Common.BaseEntity"/> produces it — a ULID —
    /// rather than being invented here.
    /// </summary>
    private static string NewId() => Ulid.NewUlid().ToString();
}
