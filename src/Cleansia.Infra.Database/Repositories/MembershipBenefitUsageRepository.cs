using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Cleansia.Infra.Database.Repositories;

public class MembershipBenefitUsageRepository(
    CleansiaDbContext context,
    ITenantProvider tenantProvider,
    IUserSessionProvider userSessionProvider)
    : BaseRepository<MembershipBenefitUsage>(context), IMembershipBenefitUsageRepository
{
    private const string SystemActor = "System";

    /// <summary>
    /// ADR-0035 D3 (AM-5 + AM-19). Two independent guards inside one statement, and they answer two
    /// different questions — neither substitutes for the other:
    ///
    /// <list type="number">
    /// <item><b>Which ordinal?</b> The SMALLEST FREE one, from <c>generate_series(0, @max-1)</c> +
    /// <c>NOT EXISTS</c> + <c>ORDER BY g LIMIT 1</c>. A <c>COUNT</c> of live rows gives the
    /// <i>cardinality</i> of the live set, not the smallest free ordinal: release slot 0 of {0,1} and a
    /// count derives 1, which collides with the live row, so <c>ON CONFLICT DO NOTHING</c> returns
    /// nothing and capacity is never restored while the read path still says "1 left".</item>
    /// <item><b>How many at all?</b> The live count in the period must be under the quota. With a
    /// constant quota this is redundant — a full quota yields no candidate ordinal — but the quota is
    /// NOT constant across a period: a mid-month plan downgrade shrinks it, and the owner ruled the
    /// count carries across the switch. Quota 4 with live {0,1,2}, downgrade to 2, release ordinal 0 ⇒
    /// <c>generate_series(0,1)</c> finds 0 free and grants a FOURTH waiver on a two-waiver plan, while
    /// the read path truthfully reports 0 remaining.</item>
    /// </list>
    ///
    /// The filtered <c>NULLS NOT DISTINCT</c> unique index behind <c>ON CONFLICT</c> is the backstop for
    /// the race the two guards cannot see: two callers evaluating the same instant.
    /// </summary>
    private const string ReserveSlotSql = """
        INSERT INTO "MembershipBenefitUsages"
            ("Id", "UserId", "BenefitKind", "PeriodKey", "UserMembershipId", "SlotOrdinal",
             "ReservedAtUtc", "IsActive", "TenantId", "CreatedBy", "CreatedOn")
        SELECT @id, @userId, @kind, @periodKey, @userMembershipId, g, @reservedAt,
               TRUE, @tenantId, @createdBy, @now
        FROM   generate_series(0, @maxPerPeriod - 1) AS g
        WHERE  NOT EXISTS (
                   SELECT 1 FROM "MembershipBenefitUsages" u
                   WHERE  u."UserId"      = @userId
                     AND  u."BenefitKind" = @kind
                     AND  u."PeriodKey"   = @periodKey
                     AND  u."TenantId" IS NOT DISTINCT FROM @tenantId
                     AND  u."IsActive"
                     AND  u."SlotOrdinal" = g)
          AND  (SELECT COUNT(*) FROM "MembershipBenefitUsages" u2
                 WHERE u2."UserId"      = @userId
                   AND u2."BenefitKind" = @kind
                   AND u2."PeriodKey"   = @periodKey
                   AND u2."TenantId" IS NOT DISTINCT FROM @tenantId
                   AND u2."IsActive") < @maxPerPeriod
        ORDER BY g
        LIMIT 1
        ON CONFLICT DO NOTHING
        RETURNING "SlotOrdinal" AS "Value";
        """;

    public async Task<MembershipBenefitUsage?> TryReserveSlotAsync(
        string userId,
        MembershipBenefitKind benefitKind,
        string periodKey,
        string userMembershipId,
        int maxPerPeriod,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (maxPerPeriod <= 0)
        {
            return null;
        }

        var tenantId = tenantProvider?.GetCurrentTenantId();
        var actorId = userSessionProvider?.GetUserId();
        var createdBy = string.IsNullOrWhiteSpace(actorId) ? SystemActor : actorId!;
        var id = Ulid.NewUlid().ToString();
        var reservedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        var parameters = new[]
        {
            new NpgsqlParameter("id", id),
            new NpgsqlParameter("userId", userId),
            new NpgsqlParameter("kind", (int)benefitKind),
            new NpgsqlParameter("periodKey", periodKey),
            new NpgsqlParameter("userMembershipId", userMembershipId),
            new NpgsqlParameter("maxPerPeriod", maxPerPeriod),
            new NpgsqlParameter("reservedAt", NpgsqlDbType.TimestampTz) { Value = reservedAtUtc },
            new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = new DateTimeOffset(reservedAtUtc) },
            // EXPLICIT type, not inferred. @tenantId is used bare in the INSERT...SELECT list (which
            // gives it no type — PostgreSQL resolves the SELECT's own output types before coercing to
            // the target columns) AND in `IS NOT DISTINCT FROM u."TenantId"` (text). Untyped with a
            // NULL value, PostgreSQL deduces two different types for the same parameter and refuses the
            // whole statement with 42P08 — in SINGLE-TENANT mode only, which is why the promo path shipped
            // this bug past a tenanted test run.
            new NpgsqlParameter("tenantId", NpgsqlDbType.Text) { Value = (object?)tenantId ?? DBNull.Value },
            new NpgsqlParameter("createdBy", createdBy),
        };

        var reservedOrdinals = await Context.Database
            .SqlQueryRaw<int>(ReserveSlotSql, parameters)
            .ToListAsync(cancellationToken);

        if (reservedOrdinals.Count == 0)
        {
            return null;
        }

        var reserved = MembershipBenefitUsage.Create(
            userId, benefitKind, periodKey, reservedOrdinals[0], userMembershipId, reservedAtUtc);
        reserved.Id = id;
        reserved.TenantId = tenantId;
        return reserved;
    }

    public Task<int> CountLiveInPeriodAsync(
        string userId,
        MembershipBenefitKind benefitKind,
        string periodKey,
        CancellationToken cancellationToken)
    {
        return GetDbSet()
            .CountAsync(
                u => u.UserId == userId
                    && u.BenefitKind == benefitKind
                    && u.PeriodKey == periodKey
                    && u.IsActive,
                cancellationToken);
    }

    public Task<MembershipBenefitUsage?> GetLiveByOrderIdAsync(
        string orderId,
        MembershipBenefitKind benefitKind,
        CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(
                u => u.OrderId == orderId && u.BenefitKind == benefitKind && u.IsActive,
                cancellationToken);
    }

    public Task<MembershipBenefitUsage?> GetTrackedByIdAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public Task<int> ReleaseOrphanedReservationsAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters: the sweep runs with no JWT, so the ambient tenant is null and the filter
        // would hide every tenanted orphan.
        return GetDbSet()
            .IgnoreQueryFilters()
            .Where(u => u.OrderId == null && u.IsActive && u.ReservedAtUtc < cutoffUtc)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false), cancellationToken);
    }
}
