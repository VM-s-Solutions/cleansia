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
