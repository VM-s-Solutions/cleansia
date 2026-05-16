using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class RecurringBookingTemplateRepository(CleansiaDbContext context)
    : BaseRepository<RecurringBookingTemplate>(context), IRecurringBookingTemplateRepository
{
    public async Task<IReadOnlyList<RecurringBookingTemplate>> GetByUserAsync(string userId, CancellationToken cancellationToken)
    {
        return await context.Set<RecurringBookingTemplate>()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.IsActive)
            .ThenByDescending(t => t.CreatedOn)
            .ToListAsync(cancellationToken);
    }
}
