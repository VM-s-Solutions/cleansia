using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class ReferralCodeRepository(CleansiaDbContext context)
    : BaseRepository<ReferralCode>(context), IReferralCodeRepository
{
    public Task<ReferralCode?> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult<ReferralCode?>(null);
        }

        return GetDbSet()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }

    public Task<ReferralCode?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult<ReferralCode?>(null);
        }

        // Codes are stored canonical-uppercase; callers normalise too, but be
        // defensive in case a hand-written lookup forgets.
        var normalised = code.Trim().ToUpperInvariant();
        return GetDbSet()
            .FirstOrDefaultAsync(c => c.Code == normalised, cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(false);
        }

        var normalised = code.Trim().ToUpperInvariant();
        return GetDbSet()
            .AnyAsync(c => c.Code == normalised, cancellationToken);
    }
}
