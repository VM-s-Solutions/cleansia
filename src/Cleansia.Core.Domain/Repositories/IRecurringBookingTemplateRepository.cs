using Cleansia.Core.Domain.Bookings;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Repository for <see cref="RecurringBookingTemplate"/>. The materializer
/// background job is the only consumer today; future Plus UX will add
/// per-user CRUD via this same interface.
/// </summary>
public interface IRecurringBookingTemplateRepository : IRepository<RecurringBookingTemplate, string>
{
    /// <summary>
    /// All templates owned by a user, both active and paused, ordered with
    /// active first then by creation date. Used by the customer Plus UI to
    /// list their schedules.
    /// </summary>
    Task<IReadOnlyList<RecurringBookingTemplate>> GetByUserAsync(string userId, CancellationToken cancellationToken);
}
