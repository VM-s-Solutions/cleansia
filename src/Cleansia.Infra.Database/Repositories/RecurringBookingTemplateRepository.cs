using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class RecurringBookingTemplateRepository(CleansiaDbContext context)
    : BaseRepository<RecurringBookingTemplate>(context), IRecurringBookingTemplateRepository
{
    public Task<RecurringBookingTemplate?> GetByIdForOwnerAsync(string id, string userId, CancellationToken cancellationToken)
        => GetQueryableIgnoringTenant().FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<RecurringBookingTemplate>> GetByUserAsync(string userId, CancellationToken cancellationToken)
    {
        // The caller's account owns schedules in every market operator.
        return await GetQueryableIgnoringTenant()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.IsActive)
            .ThenByDescending(t => t.CreatedOn)
            .ToListAsync(cancellationToken);
    }
}
