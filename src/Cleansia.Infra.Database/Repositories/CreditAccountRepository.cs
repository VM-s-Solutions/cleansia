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

    public async Task<bool> TryReturnAsync(
        string userId,
        string currencyId,
        decimal amount,
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

        // The key check is not the guarantee - the unique index below is. It is here so a replay is a
        // cheap no-op rather than an exception the caller has to catch: a re-driven refund and a
        // re-delivered webhook both arrive on a key that is already used, routinely.
        var alreadyReturned = await context.CreditTransactions
            .AsNoTracking()
            .AnyAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);
        if (alreadyReturned)
        {
            return false;
        }

        // An account may not exist yet: credit can only reach an order through one, but a customer
        // whose account was created and then erased still needs somewhere for the money to land. This
        // is the one part of a return that goes through the tracked graph, and it is followed by an
        // explicit flush so the raw statement below has a row to update.
        var account = await EnsureForUserAsync(userId, currencyId, cancellationToken);
        if (context.Entry(account).State == EntityState.Added)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        // ONE STATEMENT, mirroring TryDebitAsync, and for a second reason on top of the shared one.
        //
        // Shared: balance and ledger move together or not at all, so
        // Balance == SUM(Transactions.Amount) cannot drift.
        //
        // Its own: this must NOT enlist in the caller's unit of work. CreateOrder compensates a failed
        // Stripe dispatch by calling it on a request that then returns a FAILURE, which the pipeline
        // never commits - a tracked Issue would be thrown away along with the half-built order, and
        // flushing it explicitly would persist that order. A self-contained statement is the only
        // shape that puts the money back on a path that is about to roll back.
        //
        // TenantId is copied from the ACCOUNT rather than written as NULL: the ledger row belongs to
        // whichever tenant the balance does, and hard-coding null would be right only for as long as
        // single-tenant mode lasts.
        var rowsAffected = await context.Database.ExecuteSqlAsync(
            $"""
            WITH returned AS (
                UPDATE "CreditAccounts"
                SET "Balance" = "Balance" + {amount},
                    -- See TryDebitAsync: the raw statements push the expiry themselves.
                    "ExpiresOn" = NOW() + MAKE_INTERVAL(months => {CreditAccount.ExpiryMonths}),
                    "UpdatedBy" = {actorId},
                    "UpdatedOn" = NOW()
                WHERE "Id" = {account.Id}
                RETURNING "Id", "TenantId"
            )
            INSERT INTO "CreditTransactions" (
                "Id", "CreditAccountId", "Amount", "Reason", "OrderId", "DisputeId",
                "IdempotencyKey", "Note", "IsActive", "TenantId", "CreatedBy", "CreatedOn")
            SELECT
                {NewId()}, returned."Id", {amount}, {(int)CreditTransactionReason.OrderPaymentReturned},
                {orderId}, NULL, {idempotencyKey}, {note}, TRUE, returned."TenantId", {actorId}, NOW()
            FROM returned
            """,
            cancellationToken);

        return rowsAffected > 0;
    }

    public async Task<IReadOnlyList<CreditAccount>> GetExpiredAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken)
    {
        // IGNORING TENANT, because the sweep runs as a system job with no JWT and must see every
        // tenant's accounts. The caller groups by TenantId and commits inside the loop; a deferred
        // commit would stamp every ledger row with whichever tenant was processed last.
        return await GetQueryableIgnoringTenant()
            .Where(a => a.ExpiresOn != null && a.ExpiresOn <= asOf && a.Balance > 0m)
            .OrderBy(a => a.ExpiresOn)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetReturnedTotalForOrderAsync(
        string orderId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(orderId))
        {
            return 0m;
        }

        return await context.CreditTransactions
            .AsNoTracking()
            .Where(t => t.OrderId == orderId
                && t.Reason == CreditTransactionReason.OrderPaymentReturned)
            .SumAsync(t => t.Amount, cancellationToken);
    }

    public async Task<CreditSpendable?> GetSpendableAsync(
        string userId, CancellationToken cancellationToken)
    {
        var row = await GetDbSet()
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => new CreditSpendable(a.Id, a.Balance, a.CurrencyId, a.ExpiresOn))
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
                    -- The same push CreditAccount.Touch() does in memory. A balance that moved today
                    -- must not expire on a clock started a year ago, and these statements bypass the
                    -- entity entirely.
                    "ExpiresOn" = NOW() + MAKE_INTERVAL(months => {CreditAccount.ExpiryMonths}),
                    "UpdatedBy" = {actorId},
                    "UpdatedOn" = NOW()
                WHERE "Id" = {creditAccountId} AND "Balance" >= {amount}
                -- TenantId comes back with the row so the ledger entry belongs to the same tenant the
                -- balance does. Writing NULL was right only for as long as single-tenant mode lasts.
                RETURNING "Id", "TenantId"
            )
            INSERT INTO "CreditTransactions" (
                "Id", "CreditAccountId", "Amount", "Reason", "OrderId", "DisputeId",
                "IdempotencyKey", "Note", "IsActive", "TenantId", "CreatedBy", "CreatedOn")
            SELECT
                {NewId()}, debited."Id", {-amount}, {(int)reason}, {orderId}, NULL,
                {idempotencyKey}, {note}, TRUE, debited."TenantId", {actorId}, NOW()
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
