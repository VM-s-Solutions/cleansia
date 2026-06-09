using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Cleansia.Infra.Database.Repositories;

public class FiscalCounterRepository(
    CleansiaDbContext context,
    ITenantProvider tenantProvider,
    IUserSessionProvider userSessionProvider)
    : BaseRepository<FiscalCounter>(context), IFiscalCounterRepository
{
    private const string SystemActor = "System";

    public async Task<long> AllocateNextAsync(int year, string issuerScope, CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider?.GetCurrentTenantId();
        var actorId = userSessionProvider?.GetUserId();
        var createdBy = string.IsNullOrWhiteSpace(actorId) ? SystemActor : actorId!;
        var now = DateTimeOffset.UtcNow;
        var id = Ulid.NewUlid().ToString();

        // One statement does both first-use insert and the atomic increment. The INSERT seeds the
        // counter at 1; the ON CONFLICT branch increments the existing row, and because Postgres takes
        // a row lock on the conflicting tuple, concurrent allocations for the same scope are serialized
        // — each RETURNING reports a distinct contiguous value. Running through the context's
        // connection joins the caller's open transaction (the phase-1 claim), so the allocated number
        // is bound to the same commit/rollback as the receipt row. The unique index is NULLS NOT
        // DISTINCT, so a null TenantId (single-tenant) collapses onto one counter row instead of
        // inserting a duplicate per call.
        const string sql = """
            INSERT INTO "FiscalCounters"
                ("Id", "Year", "IssuerScope", "Value", "IsActive", "TenantId", "CreatedBy", "CreatedOn")
            VALUES (@id, @year, @scope, 1, TRUE, @tenantId, @createdBy, @now)
            ON CONFLICT ("TenantId", "Year", "IssuerScope")
            DO UPDATE SET "Value" = "FiscalCounters"."Value" + 1,
                          "UpdatedBy" = @createdBy,
                          "UpdatedOn" = @now
            RETURNING "Value";
            """;

        var parameters = new[]
        {
            new NpgsqlParameter("id", id),
            new NpgsqlParameter("year", year),
            new NpgsqlParameter("scope", issuerScope),
            new NpgsqlParameter("tenantId", (object?)tenantId ?? DBNull.Value),
            new NpgsqlParameter("createdBy", createdBy),
            new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = now },
        };

        var allocated = await Context.Database
            .SqlQueryRaw<long>(sql, parameters)
            .ToListAsync(cancellationToken);

        return allocated[0];
    }
}
