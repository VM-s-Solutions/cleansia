#nullable enable
using Cleansia.Core.AppServices.Features.Notifications.DTOs;
using Cleansia.Core.AppServices.Features.Notifications.Mappers;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using DomainSorting = Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.AppServices.Features.Notifications;

public class GetPagedUserNotifications
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    public class Request : DataRangeRequest, IRequest<PagedData<UserNotificationDto>>
    {
        public Request()
        {
            Limit = DefaultPageSize;
        }

        /// <summary>Server-enriched: always overwritten by the host controller, never trusted from the client.</summary>
        public NotificationFeedAudience Audience { get; set; }
    }

    internal class Handler(
        IUserNotificationRepository userNotificationRepository,
        IUserSessionProvider userSessionProvider)
        : IRequestHandler<Request, PagedData<UserNotificationDto>>
    {
        // The feed is always newest-first; client sort input is deliberately not honored.
        private static readonly DomainSorting.SortDefinition[] CreatedOnDescending =
        [
            new()
            {
                Field = nameof(UserNotification.CreatedOn),
                Direction = DomainSorting.SortDirection.Descending,
            },
        ];

        public async Task<PagedData<UserNotificationDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()
                         ?? throw new UnauthorizedAccessException("User ID not found in claims.");

            var page = request.Limit <= MaxPageSize
                ? request
                : new Request { Offset = request.Offset, Limit = MaxPageSize, Audience = request.Audience };

            var eventKeys = NotificationFeedEventKeys.For(page.Audience);
            var filter = UserNotificationSpecification.Create(userId, eventKeys).SatisfiedBy();

            var totalItems = await userNotificationRepository.GetCountAsync(filter, cancellationToken);
            var notifications = await userNotificationRepository
                .GetPagedSort<UserNotificationSort>(page.Offset, page.Limit, filter, CreatedOnDescending)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var items = notifications.Select(n => n.MapToDto()).ToList();
            return items.MapToDto(totalItems, page);
        }
    }
}
