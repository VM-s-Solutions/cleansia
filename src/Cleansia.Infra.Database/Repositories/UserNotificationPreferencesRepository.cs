using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class UserNotificationPreferencesRepository(CleansiaDbContext context)
    : BaseRepository<UserNotificationPreferences>(context),
      IUserNotificationPreferencesRepository
{
    public Task<UserNotificationPreferences?> GetByUserIdAsync(
        string userId, CancellationToken cancellationToken)
    {
        return context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }
}
