using Cleansia.Core.Domain.Notifications;

namespace Cleansia.Core.Domain.Repositories;

public interface IUserNotificationPreferencesRepository
    : IRepository<UserNotificationPreferences, string>
{
    Task<UserNotificationPreferences?> GetByUserIdAsync(
        string userId, CancellationToken cancellationToken);
}
