using Cleansia.Core.Domain.Notifications;

namespace Cleansia.Core.Domain.Repositories;

public interface IUserNotificationPreferencesRepository
    : IRepository<UserNotificationPreferences, string>
{
    Task<UserNotificationPreferences?> GetByUserIdAsync(
        string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The company's customers a promo reaches, ordered by user id, after <paramref name="afterUserId"/>.
    /// Explicit tenant predicate on unfiltered sets: the fan-out runs with no JWT.
    /// </summary>
    Task<List<(string UserId, string? PreferredLanguageCode)>> GetPromoRecipientsAsync(
        string? tenantId, string? afterUserId, int take, CancellationToken cancellationToken);
}
