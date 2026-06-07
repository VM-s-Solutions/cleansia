using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Notifications.DTOs;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Notifications;

/// <summary>
/// Returns the calling user's push-notification preferences. If no row
/// exists yet, lazy-creates one with defaults so the client always sees a
/// stable shape — that side effect is why the inner type is named
/// <c>Command</c> rather than <c>Query</c>: the UnitOfWorkPipelineBehavior
/// commits only when the request type name ends in "Command".
/// </summary>
public static class GetMyNotificationPreferences
{
    public record Command : ICommand<NotificationPreferencesDto>;

    // Parameterless (operates on the session user); the validator exists because the validation
    // pipeline requires one for every *Command.
    public class Validator : AbstractValidator<Command>;

    public class Handler(
        IUserNotificationPreferencesRepository repository,
        IUserSessionProvider userSessionProvider)
        : ICommandHandler<Command, NotificationPreferencesDto>
    {
        public async Task<BusinessResult<NotificationPreferencesDto>> Handle(
            Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var existing = await repository.GetByUserIdAsync(userId, cancellationToken);

            if (existing is null)
            {
                existing = UserNotificationPreferences.CreateDefaults(userId);
                repository.Add(existing);
            }

            return BusinessResult.Success(MapToDto(existing));
        }

        private static NotificationPreferencesDto MapToDto(UserNotificationPreferences entity) =>
            new(
                OrderUpdates: entity.OrderUpdates,
                CleanerOnTheWay: entity.CleanerOnTheWay,
                OrderCompleted: entity.OrderCompleted,
                OrderCancelled: entity.OrderCancelled,
                RefundIssued: entity.RefundIssued,
                MembershipExpiring: entity.MembershipExpiring,
                MembershipCancelled: entity.MembershipCancelled,
                TierUpgrade: entity.TierUpgrade,
                Promo: entity.Promo,
                DisputeReply: entity.DisputeReply,
                RecurringScheduled: entity.RecurringScheduled);
    }
}
