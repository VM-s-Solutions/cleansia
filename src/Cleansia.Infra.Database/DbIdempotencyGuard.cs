using Cleansia.Core.Domain.Messaging;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database;

/// <summary>
/// The durable backing for <see cref="IIdempotencyGuard"/> — replaces the process-local
/// <c>InMemoryIdempotencyGuard</c> so a claim survives a worker restart and spans scaled-out instances
/// (true at-most-once-after-the-marker). Mirrors the at-most-once idempotency-row pattern of
/// <c>ProcessedStripeEvent</c> and the own-commit discipline of <see cref="DeadLetterStore"/>.
///
/// <para>The claim OWNS ITS OWN COMMIT: the queue consumer that calls this has no MediatR
/// <c>UnitOfWork</c> pipeline, and its only later commit is the post-send dead-token prune — the claim
/// must NOT wait on (or be rolled back with) that deferrable business commit, which is the whole point:
/// the claim is durable even if the terminal effect later crashes. Registered scoped, it resolves on the
/// invocation's scoped <see cref="CleansiaDbContext"/>; the independent commit here means a sibling
/// deferred commit on the same context never rolls the claim back.</para>
/// </summary>
public class DbIdempotencyGuard(IProcessedMessageRepository repository) : IIdempotencyGuard
{
    public async Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default)
    {
        // Fast path: the common redelivery case (the key was claimed on an earlier attempt) short-circuits
        // here, so we don't attempt an insert that would fail the unique index — which EF logs as a noisy
        // "Failed executing DbCommand" Error even though the guard handles it. Not a substitute for the
        // insert-catch below (two parallel redeliveries can both miss this) — it just removes the noise
        // and a wasted round-trip in the overwhelmingly common single-redelivery case.
        if (await repository.HasProcessedAsync(messageKey, ct))
        {
            return true;
        }

        repository.Add(ProcessedMessage.Create(messageKey));
        try
        {
            // Claim in its OWN unit of work — flushed independently of any later business commit.
            await repository.CommitAsync(ct);
            return false; // Won the claim — the caller proceeds with its terminal effect.
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The unique index on MessageKey rejected this insert: a concurrent consumer claimed the key
            // between our existence check and this insert (the rare two-parallel-redeliveries race).
            // Already claimed → the caller acks WITHOUT re-running the effect. Genuine infra faults are not
            // unique-violations, so they re-throw and the queue retries.
            return true;
        }
    }

    public Task<bool> HasProcessedAsync(string messageKey, CancellationToken ct = default) =>
        repository.HasProcessedAsync(messageKey, ct);

    // The act-then-claim caller only needs the row to exist afterwards, so this reuses the claim above
    // verbatim (existence fast path, own-commit insert, unique-violation collapse on a concurrent claim)
    // and discards the won/lost distinction. Genuine infra faults still propagate.
    public Task MarkProcessedAsync(string messageKey, CancellationToken ct = default) =>
        AlreadyProcessedAsync(messageKey, ct);

    /// <summary>
    /// True when the <see cref="DbUpdateException"/> was caused by a unique-constraint violation. Detected
    /// provider-agnostically by duck-typing the inner exception chain (this layer carries no hard Npgsql
    /// reference): Postgres surfaces <c>PostgresException.SqlState == "23505"</c>; SQLite (the test backend
    /// running the real model + real unique index) surfaces <c>SqliteException.SqliteErrorCode == 19</c>
    /// (SQLITE_CONSTRAINT). Mirrors the duck-typed <c>IsUniqueViolation</c> helpers used by the receipt /
    /// loyalty / refund / membership idempotency paths.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        const string PostgresUniqueViolation = "23505";
        const int SqliteConstraintErrorCode = 19; // SQLITE_CONSTRAINT — the real unique index in tests.

        for (Exception? inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            var type = inner.GetType();

            if (type.GetProperty("SqlState")?.GetValue(inner) as string == PostgresUniqueViolation)
            {
                return true;
            }

            if (type.GetProperty("SqliteErrorCode")?.GetValue(inner) is int code && code == SqliteConstraintErrorCode)
            {
                return true;
            }
        }

        return false;
    }
}
