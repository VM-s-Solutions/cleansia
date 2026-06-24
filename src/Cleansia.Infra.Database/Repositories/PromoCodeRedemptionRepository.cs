using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Cleansia.Infra.Database.Repositories;

public class PromoCodeRedemptionRepository(
    CleansiaDbContext context,
    ITenantProvider tenantProvider,
    IUserSessionProvider userSessionProvider)
    : BaseRepository<PromoCodeRedemption>(context), IPromoCodeRedemptionRepository
{
    private const string SystemActor = "System";

    public Task<int> CountForUserAndCodeAsync(string userId, string promoCodeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .CountAsync(r => r.UserId == userId && r.PromoCodeId == promoCodeId, cancellationToken);
    }

    public Task<PromoCodeRedemption?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);
    }

    public async Task<PromoCodeRedemption?> TryReserveRedemptionSlotAsync(
        string userId,
        string promoCodeId,
        int maxRedemptionsPerUser,
        string orderId,
        decimal appliedDiscount,
        CancellationToken cancellationToken)
    {
        // S7 — ATOMIC per-user slot reservation. The next 0-based SlotOrdinal is computed
        // in SQL as COALESCE(MAX(SlotOrdinal) + 1, 0) over this (tenant, code, user) and the whole
        // INSERT is gated by a HAVING < maxRedemptionsPerUser guard, so:
        //   - the ordinal is DERIVED from the reservation (never a pre-read count — that would let
        //     two concurrent M>1 redemptions collide on the same ordinal and falsely reject one);
        //   - the per-user cap is enforced by the database in a single statement, closing the
        //     read-then-insert race.
        // The (TenantId, PromoCodeId, UserId, SlotOrdinal) unique index is a defense-in-depth
        // BACKSTOP: ON CONFLICT DO NOTHING turns a concurrent same-ordinal insert into 0 rows
        // returned (a clean RESULT), NOT an exception surfaced at a later commit.
        //
        // DELIBERATE EXCEPTION to the "never CommitAsync outside the UnitOfWork pipeline" rule:
        // this raw INSERT auto-commits immediately and is NOT change-tracked. It is the ONLY direct
        // DB write in the redeem path and is REQUIRED for atomicity — the reservation must land (or
        // be rejected) on its own, not deferred to the order's UoW commit (a unique violation there
        // would roll back the whole paid order, which is worse than the bug; see CreateOrder
        // fail-soft). A null result simply logs; the order is untouched.
        var tenantId = tenantProvider?.GetCurrentTenantId();
        var actorId = userSessionProvider?.GetUserId();
        var createdBy = string.IsNullOrWhiteSpace(actorId) ? SystemActor : actorId!;
        var now = DateTimeOffset.UtcNow;
        var id = Ulid.NewUlid().ToString();

        const string sql = """
            INSERT INTO "PromoCodeRedemptions"
                ("Id", "PromoCodeId", "UserId", "OrderId", "AppliedDiscount", "RedeemedOn",
                 "SlotOrdinal", "IsActive", "TenantId", "CreatedBy", "CreatedOn")
            SELECT @id, @codeId, @userId, @orderId, @discount, @now,
                   COALESCE(MAX(r."SlotOrdinal") + 1, 0),
                   TRUE, @tenantId, @createdBy, @now
            FROM "PromoCodeRedemptions" r
            WHERE r."PromoCodeId" = @codeId
              AND r."UserId" = @userId
              AND r."TenantId" IS NOT DISTINCT FROM @tenantId
            HAVING COALESCE(MAX(r."SlotOrdinal") + 1, 0) < @maxPerUser
            ON CONFLICT DO NOTHING
            RETURNING "SlotOrdinal" AS "Value";
            """;

        var parameters = new[]
        {
            new NpgsqlParameter("id", id),
            new NpgsqlParameter("codeId", promoCodeId),
            new NpgsqlParameter("userId", userId),
            new NpgsqlParameter("orderId", orderId),
            new NpgsqlParameter("discount", appliedDiscount),
            new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = now },
            new NpgsqlParameter("maxPerUser", maxRedemptionsPerUser),
            new NpgsqlParameter("tenantId", (object?)tenantId ?? DBNull.Value),
            new NpgsqlParameter("createdBy", createdBy),
        };

        // SqlQueryRaw materialises the RETURNING projection. 0 rows ⇒ cap reached or a race loser
        // (ON CONFLICT) ⇒ no slot ⇒ null. >0 rows ⇒ the reserved ordinal.
        var reservedOrdinals = await Context.Database
            .SqlQueryRaw<int>(sql, parameters)
            .ToListAsync(cancellationToken);

        if (reservedOrdinals.Count == 0)
        {
            return null;
        }

        return PromoCodeRedemption.CreateReserved(
            promoCodeId, userId, orderId, appliedDiscount, reservedOrdinals[0]);
    }

    public Task<int> CountByPromoCodeAsync(string promoCodeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .CountAsync(r => r.PromoCodeId == promoCodeId, cancellationToken);
    }
}
