using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
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

    public async Task<List<(string UserId, string? PreferredLanguageCode)>> GetPromoRecipientsAsync(
        string? tenantId, string? afterUserId, int take, CancellationToken cancellationToken)
    {
        var recipients = context.UserNotificationPreferences.IgnoreQueryFilters()
            .Where(p => p.Promo && p.TenantId == tenantId)
            .Join(context.Users.IgnoreQueryFilters()
                    .Where(u => u.IsActive
                        && u.Profile == UserProfile.Customer
                        && !u.Email.EndsWith(User.AnonymisedEmailSuffix)),
                p => p.UserId,
                u => u.Id,
                (p, u) => new { u.Id, u.PreferredLanguageCode });

        if (afterUserId is not null)
        {
            recipients = recipients.Where(x => string.Compare(x.Id, afterUserId) > 0);
        }

        var page = await recipients.OrderBy(x => x.Id).Take(take).ToListAsync(cancellationToken);
        return page.Select(x => (x.Id, x.PreferredLanguageCode)).ToList();
    }
}
