using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Cleansia.Infra.Database.Repositories;

public class PayoutReferenceCounterRepository(
    CleansiaDbContext context,
    IUserSessionProvider userSessionProvider)
    : BaseRepository<PayoutReferenceCounter>(context), IPayoutReferenceCounterRepository
{
    private const string SystemActor = "System";

    private const long MaxOrdinalPerYear = 999999;

    public async Task<long?> AllocateNextAsync(int year, CancellationToken cancellationToken)
    {
        var actorId = userSessionProvider?.GetUserId();
        var createdBy = string.IsNullOrWhiteSpace(actorId) ? SystemActor : actorId!;
        var now = DateTimeOffset.UtcNow;
        var id = Ulid.NewUlid().ToString();

        // One statement does both first-use insert and the atomic increment. The INSERT seeds the
        // counter at 1; the ON CONFLICT branch increments the existing row, and because Postgres takes
        // a row lock on the conflicting tuple, concurrent allocations for the same year are serialized
        // — each RETURNING reports a distinct value. The arbiter is ("Year") alone: a non-nullable int
        // cannot reproduce the nulls-distinct collapse FiscalCounters has to answer with
        // AreNullsDistinct(false), and there is deliberately no tenant or scope term (ADR-0046 §D2.1).
        //
        // DELIBERATE EXCEPTION to the "never CommitAsync outside the UnitOfWork pipeline" rule, in the
        // PromoCodeRepository.TryIncrementGlobalRedemptionsAsync shape: this statement auto-commits
        // immediately — it is NOT change-tracked and is NOT part of the unit of work the calling
        // handler commits at the end. That is intentional and REQUIRED: the number must be claimed
        // before any row or document can carry it, independently of whether that row ever commits. It
        // does NOT roll back with the caller — an invoice that fails to commit leaves a gap in the
        // sequence, which is correct for a payment reference (it is not a fiscal document number, and
        // nothing here requires gaplessness). The self-commit holds only because no payout path opens
        // an explicit transaction; SqlQueryRaw would otherwise JOIN one, which is why "never call this
        // inside a transaction" is an invariant on the interface rather than an assumption here.
        //
        // The cap lives in the WHERE, not in a later C# check: without it the counter runs permanently
        // past 999999 and formats to eleven digits, platform-wide, repairable only by a manual UPDATE.
        // When the guard is false the DO UPDATE affects no row and RETURNING yields NOTHING — hence
        // FirstOrDefault over a list rather than FiscalCounterRepository.cs's unguarded allocated[0],
        // which would throw ArgumentOutOfRangeException from inside a repository at the cap.
        const string sql = """
            INSERT INTO "PayoutReferenceCounters"
                ("Id", "Year", "Value", "IsActive", "CreatedBy", "CreatedOn")
            VALUES (@id, @year, 1, TRUE, @createdBy, @now)
            ON CONFLICT ("Year")
            DO UPDATE SET "Value" = "PayoutReferenceCounters"."Value" + 1,
                          "UpdatedBy" = @createdBy,
                          "UpdatedOn" = @now
            WHERE "PayoutReferenceCounters"."Value" < @maxValue
            RETURNING "Value";
            """;

        var parameters = new[]
        {
            new NpgsqlParameter("id", id),
            new NpgsqlParameter("year", year),
            new NpgsqlParameter("createdBy", createdBy),
            new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = now },
            new NpgsqlParameter("maxValue", MaxOrdinalPerYear),
        };

        var allocated = await Context.Database
            .SqlQueryRaw<long>(sql, parameters)
            .ToListAsync(cancellationToken);

        return allocated.Count == 0 ? null : allocated[0];
    }
}
