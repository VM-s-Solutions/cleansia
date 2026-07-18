using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Notifications.DTOs;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Notifications;

public class GetUnreadNotificationCount
{
    public record Query(NotificationFeedAudience Audience) : IQuery<UnreadNotificationCountDto>;

    public class Handler(
        IUserNotificationRepository userNotificationRepository,
        IUserSessionProvider userSessionProvider) : IQueryHandler<Query, UnreadNotificationCountDto>
    {
        public async Task<BusinessResult<UnreadNotificationCountDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()
                         ?? throw new UnauthorizedAccessException("User ID not found in claims.");

            var count = await userNotificationRepository.GetUnreadCountAsync(
                userId,
                NotificationFeedEventKeys.For(query.Audience),
                cancellationToken);

            return BusinessResult.Success(new UnreadNotificationCountDto(count));
        }
    }
}
