using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class SavedAddressRepository(CleansiaDbContext context, IUserSessionProvider userSessionProvider)
    : BaseRepository<SavedAddress>(context), ISavedAddressRepository
{
    private const string SystemActor = "System";

    public async Task<IReadOnlyList<SavedAddress>> GetByUserAsync(string userId, CancellationToken cancellationToken)
    {
        return await context.Set<SavedAddress>()
            .Include(s => s.Address)
                .ThenInclude(a => a!.Country)
            .Where(s => s.UserId == userId)
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.IsDefault)
            .ThenBy(s => s.Label)
            .ToListAsync(cancellationToken);
    }

    public Task<SavedAddress?> GetDefaultForUserAsync(string userId, CancellationToken cancellationToken)
    {
        return context.Set<SavedAddress>()
            .Include(s => s.Address)
                .ThenInclude(a => a!.Country)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsDefault && s.IsActive, cancellationToken);
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

    public override void Deactivate(SavedAddress entity)
    {
        var actorId = userSessionProvider?.GetUserId();
        var deactivatedBy = string.IsNullOrWhiteSpace(actorId) ? SystemActor : actorId!;
        entity.Deactivated(deactivatedBy, DateTimeOffset.UtcNow);
    }
}
