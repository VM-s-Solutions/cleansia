using Cleansia.Core.Domain.Notifications;

namespace Cleansia.Core.Domain.Repositories;

public interface IUserNotificationRepository : IRepository<UserNotification, string>
{
    /// <summary>
    /// The digest-collapse lookup: the user's single unread row for <paramref name="eventKey"/>.
    /// Tenant-filter-bypassed because producers run without a JWT (background sweeps, webhooks);
    /// the read stays pinned to one user's rows, so it never widens across users.
    /// </summary>
    Task<UserNotification?> GetUnreadByUserAndEventAsync(string userId, string eventKey, CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(string userId, IReadOnlyList<string> eventKeys, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically stamps <c>ReadOn</c> on the caller's unread rows within <paramref name="eventKeys"/>
    /// whose <c>CreatedOn</c> is at or before <paramref name="upToCreatedOn"/> (null = all of them —
    /// the watermark keeps a row created after the client's fetch unread). Returns rows affected.
    /// </summary>
    Task<int> MarkAllReadAsync(
        string userId,
        IReadOnlyList<string> eventKeys,
        DateTimeOffset? upToCreatedOn,
        DateTimeOffset readOn,
        CancellationToken cancellationToken);
}
