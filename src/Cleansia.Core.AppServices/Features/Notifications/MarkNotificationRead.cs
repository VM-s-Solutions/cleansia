using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Notifications;

public class MarkNotificationRead
{
    /// <summary><c>Audience</c> is server-enriched: always overwritten by the host controller, never trusted from the client.</summary>
    public record Command(
        string Id,
        NotificationFeedAudience Audience = NotificationFeedAudience.Customer) : ICommand<Response>;

    public record Response(string Id, DateTimeOffset ReadOn);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IUserNotificationRepository userNotificationRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()
                         ?? throw new UnauthorizedAccessException("User ID not found in claims.");

            var notification = await userNotificationRepository.GetByIdAsync(command.Id, cancellationToken);
            if (notification is null
                || notification.UserId != userId
                || !NotificationFeedEventKeys.For(command.Audience).Contains(notification.EventKey))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.Id), BusinessErrorMessage.NotFound));
            }

            notification.MarkRead(DateTimeOffset.UtcNow);

            return BusinessResult.Success(new Response(notification.Id, notification.ReadOn!.Value));
        }
    }
}
