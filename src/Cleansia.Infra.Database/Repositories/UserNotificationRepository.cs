using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class UserNotificationRepository(CleansiaDbContext context)
    : BaseRepository<UserNotification>(context), IUserNotificationRepository
{
    public Task<UserNotification?> GetUnreadByUserAndEventAsync(string userId, string eventKey, CancellationToken cancellationToken)
    {
        // Producers run without a JWT (background sweeps, webhooks), so the ambient tenant filter
        // would hide the row; the read stays pinned to a single user's rows (the sanctioned
        // cross-tenant background pattern).
        return GetQueryableIgnoringTenant()
            .FirstOrDefaultAsync(
                n => n.UserId == userId && n.EventKey == eventKey && n.ReadOn == null,
                cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(string userId, IReadOnlyList<string> eventKeys, CancellationToken cancellationToken)
    {
        return GetQueryable()
            .CountAsync(
                n => n.UserId == userId && n.ReadOn == null && eventKeys.Contains(n.EventKey),
                cancellationToken);
    }

    public Task<int> MarkAllReadAsync(
        string userId,
        IReadOnlyList<string> eventKeys,
        DateTimeOffset? upToCreatedOn,
        DateTimeOffset readOn,
        CancellationToken cancellationToken)
    {
        var unread = GetQueryable()
            .Where(n => n.UserId == userId && n.ReadOn == null && eventKeys.Contains(n.EventKey));

        if (upToCreatedOn is not null)
        {
            var upTo = upToCreatedOn.Value;
            unread = unread.Where(n => n.CreatedOn <= upTo);
        }

        return unread.ExecuteUpdateAsync(
            setters => setters.SetProperty(n => n.ReadOn, readOn),
            cancellationToken);
    }
}
