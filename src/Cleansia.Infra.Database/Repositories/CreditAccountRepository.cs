using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class CreditAccountRepository(CleansiaDbContext context)
    : BaseRepository<CreditAccount>(context), ICreditAccountRepository
{
    public async Task LockForUserAsync(string userId, CancellationToken cancellationToken)
    {
        await context.LockCreditOwnerAsync(userId, cancellationToken);
        if (context.Database.IsNpgsql())
            await context.Database.ExecuteSqlAsync(
                $"""SELECT "Id" FROM "CreditAccounts" WHERE "UserId" = {userId} ORDER BY "Id" FOR NO KEY UPDATE""",
                cancellationToken);

        // Expiry loads a batch before locking; use balances read after any preceding return/deletion.
        foreach (var entry in context.ChangeTracker.Entries<CreditAccount>()
                     .Where(e => e.Entity.UserId == userId && e.State == EntityState.Unchanged).ToList())
            await entry.ReloadAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CreditAccount>> GetAllForUserAsync(
        string userId, CancellationToken cancellationToken)
    {
        // Ordered so the two callers render and drain deterministically rather than in whatever order
        // Postgres returns. Largest balance first, then by currency, so the account the admin sees on a
        // one-balance screen is the one that matters most and does not move between page loads.
        return await GetDbSet()
            .Include(a => a.Transactions)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Balance)
            .ThenBy(a => a.CurrencyId)
            .ToListAsync(cancellationToken);
    }

    public async Task<CreditAccount?> EnsureForUserAsync(
        string userId, string currencyId, CancellationToken cancellationToken)
    {
        await LockForUserAsync(userId, cancellationToken);
        var eligible = await context.Users.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(u => u.Id == userId && (u.IsActive || !u.Email.EndsWith(User.AnonymisedEmailSuffix)),
                cancellationToken);
        if (!eligible)
            return null;

        // No Include(Transactions). Issue() only APPENDS, and EF tracks an appended child without the
        // collection pre-loaded - same reasoning as LoyaltyAccountRepository, and it matters more here
        // because a long-lived customer's ledger is unbounded.
        // BOTH COLUMNS. Matching on UserId alone returned an account in whatever currency the
        // customer's first credit happened to open, and every caller then added its own currency's
        // number to that balance -- a CZK refund landing on a EUR account, with no error and no clue.
        //
        // Past the tenant filter, and a new row stamped with the USER's company rather than the ambient
        // one: credit follows the account, but the callers that put money on it act as the ORDER's
        // operator (a refund an admin issues, the no-cleaner sweep, a cancellation), and for a booking
        // made across the border that is another company. Filtered, the read found nothing and the
        // insert collided on IX_CreditAccounts_UserId_CurrencyId, which has no tenant term on purpose.
        // Every caller reaches here with a user id read off an order or a customer it has already
        // loaded and access-checked (S8).
        var existing = await GetQueryableIgnoringTenant()
            .FirstOrDefaultAsync(
                a => a.UserId == userId && a.CurrencyId == currencyId, cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var account = CreditAccount.Create(userId, currencyId, userId);
        account.TenantId = await context.UserTenantIdAsync(userId, cancellationToken);
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

        // No tracked flush: checkout compensation must survive a failed command without saving its order.
        await using var transaction = context.Database.CurrentTransaction is null && System.Transactions.Transaction.Current is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await context.Database.ExecuteSqlAsync(
            $"""SELECT "Id" FROM "Users" WHERE "Id" = {userId} FOR NO KEY UPDATE""", cancellationToken);
        var owner = await context.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == userId && (u.IsActive || !u.Email.EndsWith(User.AnonymisedEmailSuffix)))
            .Select(u => new { u.TenantId })
            .SingleOrDefaultAsync(cancellationToken);
        if (owner is null)
            return false;

        // Read after the owner lock: a concurrent return's key is now visible.
        var alreadyReturned = await context.CreditTransactions
            .AsNoTracking()
            .AnyAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);
        if (alreadyReturned)
        {
            return false;
        }

        var rowsAffected = await context.Database.ExecuteSqlAsync(
            $"""
            WITH returned AS (
                INSERT INTO "CreditAccounts" (
                    "Id", "UserId", "CurrencyId", "TenantId", "Balance", "ExpiresOn",
                    "IsActive", "CreatedBy", "CreatedOn")
                VALUES ({NewId()}, {userId}, {currencyId}, {owner.TenantId}, {amount},
                    NOW() + MAKE_INTERVAL(months => {CreditAccount.ExpiryMonths}), TRUE, {actorId}, NOW())
                ON CONFLICT ("UserId", "CurrencyId") DO UPDATE
                SET "Balance" = "CreditAccounts"."Balance" + EXCLUDED."Balance",
                    "ExpiresOn" = EXCLUDED."ExpiresOn",
                    "UpdatedBy" = {actorId},
                    "UpdatedOn" = NOW()
                RETURNING "Id"
            )
            INSERT INTO "CreditTransactions" (
                "Id", "CreditAccountId", "Amount", "Reason", "OrderId", "DisputeId",
                "IdempotencyKey", "Note", "IsActive", "CreatedBy", "CreatedOn")
            SELECT
                {NewId()}, returned."Id", {amount}, {(int)CreditTransactionReason.OrderPaymentReturned},
                {orderId}, NULL, {idempotencyKey}, {note}, TRUE, {actorId}, NOW()
            FROM returned
            """,
            cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
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

    public async Task<IReadOnlyDictionary<string, decimal>> GetReturnedTotalsByOrderAsync(
        IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return new Dictionary<string, decimal>(0);
        }

        var rows = await context.CreditTransactions
            .AsNoTracking()
            .Where(t => t.OrderId != null
                && orderIds.Contains(t.OrderId)
                && t.Reason == CreditTransactionReason.OrderPaymentReturned)
            .GroupBy(t => t.OrderId!)
            .Select(g => new { OrderId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.OrderId, r => r.Total);
    }

    public Task<CreditSpendable?> GetSpendableAsync(
        string userId, string currencyId, CancellationToken cancellationToken)
    {
        // The user id comes from the caller's account or an authorized order.
        return GetQueryableIgnoringTenant()
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.CurrencyId == currencyId)
            .Select(a => new CreditSpendable(a.Id, a.Balance, a.CurrencyId, a.ExpiresOn))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CreditSpendable>> GetSpendablesForUserAsync(
        string userId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Balance)
            .ThenBy(a => a.CurrencyId)
            .Select(a => new CreditSpendable(a.Id, a.Balance, a.CurrencyId, a.ExpiresOn))
            .ToListAsync(cancellationToken);
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
                RETURNING "Id"
            )
            INSERT INTO "CreditTransactions" (
                "Id", "CreditAccountId", "Amount", "Reason", "OrderId", "DisputeId",
                "IdempotencyKey", "Note", "IsActive", "CreatedBy", "CreatedOn")
            SELECT
                {NewId()}, debited."Id", {-amount}, {(int)reason}, {orderId}, NULL,
                {idempotencyKey}, {note}, TRUE, {actorId}, NOW()
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
