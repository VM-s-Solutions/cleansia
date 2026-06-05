using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class PromoCodeRepository(CleansiaDbContext context)
    : BaseRepository<PromoCode>(context), IPromoCodeRepository
{
    public Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult<PromoCode?>(null);
        }

        // Codes are stored canonical-uppercase; callers normalise too, but be
        // defensive in case a hand-written lookup forgets.
        var normalised = code.Trim().ToUpperInvariant();
        return GetDbSet()
            .FirstOrDefaultAsync(c => c.Code == normalised, cancellationToken);
    }

    public async Task<bool> TryIncrementGlobalRedemptionsAsync(
        string promoCodeId,
        CancellationToken cancellationToken)
    {
        // S7 — atomic conditional increment of the denormalised global counter. This is a
        // single SQL UPDATE that bumps CurrentRedemptionsCount only while it is still below the cap
        // (or the cap is null/unlimited), so the global cap is enforced by the database, not by a
        // read-then-increment in the app layer.
        //
        // DELIBERATE EXCEPTION to the "never CommitAsync outside the UnitOfWork pipeline" rule:
        // ExecuteUpdateAsync issues SQL and auto-commits immediately — it is NOT change-tracked and
        // is NOT part of the UoW transaction the CreateOrder handler commits at the end. That is
        // intentional and REQUIRED for atomicity: the increment must land (or be rejected) on its
        // own, independently of the order commit. It does not roll the order back — it returns
        // false when the cap is reached, which the service maps to GlobalLimitReached.
        var rowsAffected = await GetQueryable()
            .Where(c => c.Id == promoCodeId
                && (c.GlobalMaxRedemptions == null
                    || c.CurrentRedemptionsCount < c.GlobalMaxRedemptions))
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.CurrentRedemptionsCount, c => c.CurrentRedemptionsCount + 1),
                cancellationToken);

        return rowsAffected > 0;
    }

    public async Task DecrementGlobalRedemptionsAsync(
        string promoCodeId,
        CancellationToken cancellationToken)
    {
        // Compensating decrement. The redeem path reserves the global slot (increment)
        // BEFORE the per-user slot; when the per-user reservation fails we must release the global slot,
        // or the global cap leaks one slot per failed reservation. Atomic single UPDATE, floored at 0
        // (GREATEST guard) so a concurrent reset can't drive the counter negative. Same deliberate
        // out-of-pipeline auto-commit as the increment — it must land independently of the order commit.
        await GetQueryable()
            .Where(c => c.Id == promoCodeId && c.CurrentRedemptionsCount > 0)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.CurrentRedemptionsCount, c => c.CurrentRedemptionsCount - 1),
                cancellationToken);
    }

    public async Task<(IReadOnlyList<PromoCode> Items, int Total)> GetPagedAdminAsync(
        bool? active,
        bool? expired,
        string? searchCode,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var query = GetDbSet().AsNoTracking();

        if (active.HasValue)
        {
            query = active.Value
                ? query.Where(c => c.IsActive)
                : query.Where(c => !c.IsActive);
        }

        if (expired.HasValue)
        {
            query = expired.Value
                ? query.Where(c => c.ValidUntil != null && c.ValidUntil < now)
                : query.Where(c => c.ValidUntil == null || c.ValidUntil >= now);
        }

        if (!string.IsNullOrWhiteSpace(searchCode))
        {
            var needle = searchCode.Trim().ToUpperInvariant();
            query = query.Where(c => EF.Functions.Like(c.Code, $"%{needle}%"));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedOn)
            .Skip(offset)
            .Take(limit)
            .Include(c => c.Currency)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
