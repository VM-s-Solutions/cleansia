using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Notifications;

public class MarkAllNotificationsRead
{
    /// <summary>
    /// <c>UpToCreatedOn</c> is the client's watermark — the newest <c>CreatedOn</c> it fetched —
    /// so a row created after the fetch stays unread (null = mark everything).
    /// <c>Audience</c> is server-enriched: always overwritten by the host controller.
    /// </summary>
    public record Command(
        DateTimeOffset? UpToCreatedOn = null,
        NotificationFeedAudience Audience = NotificationFeedAudience.Customer) : ICommand<Response>;

    public record Response(int MarkedCount);

    public class Validator : AbstractValidator<Command>
    {
        // Deliberately empty — both fields are optional and any value is legal, but the validation
        // pipeline demands a registered validator for every Command type.
    }

    public class Handler(
        IUserNotificationRepository userNotificationRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()
                         ?? throw new UnauthorizedAccessException("User ID not found in claims.");

            var marked = await userNotificationRepository.MarkAllReadAsync(
                userId,
                NotificationFeedEventKeys.For(command.Audience),
                command.UpToCreatedOn,
                DateTimeOffset.UtcNow,
                cancellationToken);

            return BusinessResult.Success(new Response(marked));
        }
    }
}
