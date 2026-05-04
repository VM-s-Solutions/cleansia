using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class SavedAddressRepository(CleansiaDbContext context)
    : BaseRepository<SavedAddress>(context), ISavedAddressRepository
{
    public async Task<IReadOnlyList<SavedAddress>> GetByUserAsync(string userId, CancellationToken cancellationToken)
    {
        return await context.Set<SavedAddress>()
            .Include(s => s.Address)
                .ThenInclude(a => a!.Country)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.IsDefault)
            .ThenBy(s => s.Label)
            .ToListAsync(cancellationToken);
    }

    public Task<SavedAddress?> GetDefaultForUserAsync(string userId, CancellationToken cancellationToken)
    {
        return context.Set<SavedAddress>()
            .Include(s => s.Address)
                .ThenInclude(a => a!.Country)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsDefault, cancellationToken);
    }

    public async Task ClearDefaultForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var existingDefaults = await context.Set<SavedAddress>()
            .Where(s => s.UserId == userId && s.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var sa in existingDefaults)
        {
            sa.SetDefault(false);
        }
    }
}
