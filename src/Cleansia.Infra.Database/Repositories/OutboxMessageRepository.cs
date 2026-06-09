using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

/// <summary>
/// The durable <see cref="OutboxMessage"/> repository. Auto-registered by the assembly-scan in
/// <c>RepositoryExtensions</c> (it implements <see cref="IRepository{TEntity,TKey}"/>). The claim is the
/// one query that needs raw SQL: a single atomic UPDATE under a row-level lock that skips already-locked
/// rows, so two drainers never grab the same row.
/// </summary>
public class OutboxMessageRepository(CleansiaDbContext context)
    : BaseRepository<OutboxMessage>(context), IOutboxMessageRepository
{
    private const string PostgresProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingBatchAsync(
        string claimToken,
        int batchSize,
        DateTimeOffset now,
        DateTimeOffset leaseCutoff,
        CancellationToken cancellationToken)
    {
        return Context.Database.ProviderName == PostgresProvider
            ? await ClaimWithSkipLockedAsync(claimToken, batchSize, now, leaseCutoff, cancellationToken)
            : await ClaimWithTrackingAsync(claimToken, batchSize, now, leaseCutoff, cancellationToken);
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimWithSkipLockedAsync(
        string claimToken, int batchSize, DateTimeOffset now, DateTimeOffset leaseCutoff, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE "OutboxMessages" SET
                "ClaimedBy" = {0},
                "ClaimedOn" = {1}
            WHERE "Id" IN (
                SELECT "Id" FROM "OutboxMessages"
                WHERE "Status" = {2}
                  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {1})
                  AND ("ClaimedOn" IS NULL OR "ClaimedOn" <= {3})
                ORDER BY "CreatedOn", "Id"
                LIMIT {4}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING *;
            """;

        // UPDATE ... RETURNING * is non-composable: EF must append nothing over it. The global tenant
        // query filter (OutboxMessage is ITenantEntity) WOULD be appended as a WHERE and force composition,
        // so IgnoreQueryFilters() — the drainer is a system process that must see every tenant's messages,
        // and the SQL already scopes by status/lease. AsAsyncEnumerable() then materialises with no further
        // LINQ. Rows are change-tracked by default (DbSet FromSql).
        var claimed = new List<OutboxMessage>();
        var rows = GetDbSet()
            .FromSqlRaw(
                sql,
                claimToken,
                now,
                (int)OutboxMessageStatus.Pending,
                leaseCutoff,
                batchSize)
            .IgnoreQueryFilters()
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken);

        await foreach (var row in rows)
        {
            claimed.Add(row);
        }

        return claimed;
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimWithTrackingAsync(
        string claimToken, int batchSize, DateTimeOffset now, DateTimeOffset leaseCutoff, CancellationToken cancellationToken)
    {
        var eligible = await GetDbSet()
            .Where(m => m.Status == OutboxMessageStatus.Pending
                        && (m.NextAttemptAt == null || m.NextAttemptAt <= now)
                        && (m.ClaimedOn == null || m.ClaimedOn <= leaseCutoff))
            .OrderBy(m => m.CreatedOn).ThenBy(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var row in eligible)
        {
            row.Claim(claimToken, now);
        }

        return eligible;
    }
}
