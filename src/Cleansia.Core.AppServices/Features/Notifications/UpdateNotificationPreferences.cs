using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Notifications.DTOs;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Notifications;

/// <summary>
/// Replace-all PUT for the calling user's push preferences. Lazy-creates
/// the row when missing; otherwise sets each category from the supplied
/// values. Same upsert semantics as <see cref="GetMyNotificationPreferences"/>
/// so the client never has to call GET first.
/// </summary>
public static class UpdateNotificationPreferences
{
    public record Command(
        bool OrderUpdates,
        bool CleanerOnTheWay,
        bool OrderCompleted,
        bool OrderCancelled,
        bool RefundIssued,
        bool MembershipExpiring,
        bool MembershipCancelled,
        bool TierUpgrade,
        bool Promo,
        bool DisputeReply,
        bool RecurringScheduled) : ICommand<NotificationPreferencesDto>;

    // All fields are bools (every combination is valid); the validator exists because the validation
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
            var preferences = await repository.GetByUserIdAsync(userId, cancellationToken);

            if (preferences is null)
            {
                preferences = UserNotificationPreferences.CreateDefaults(userId);
                repository.Add(preferences);
            }

            preferences.Set(NotificationCategory.OrderUpdates, command.OrderUpdates);
            preferences.Set(NotificationCategory.CleanerOnTheWay, command.CleanerOnTheWay);
            preferences.Set(NotificationCategory.OrderCompleted, command.OrderCompleted);
            preferences.Set(NotificationCategory.OrderCancelled, command.OrderCancelled);
            preferences.Set(NotificationCategory.RefundIssued, command.RefundIssued);
            preferences.Set(NotificationCategory.MembershipExpiring, command.MembershipExpiring);
            preferences.Set(NotificationCategory.MembershipCancelled, command.MembershipCancelled);
            preferences.Set(NotificationCategory.TierUpgrade, command.TierUpgrade);
            preferences.Set(NotificationCategory.Promo, command.Promo);
            preferences.Set(NotificationCategory.DisputeReply, command.DisputeReply);
            preferences.Set(NotificationCategory.RecurringScheduled, command.RecurringScheduled);

            return BusinessResult.Success(new NotificationPreferencesDto(
                OrderUpdates: preferences.OrderUpdates,
                CleanerOnTheWay: preferences.CleanerOnTheWay,
                OrderCompleted: preferences.OrderCompleted,
                OrderCancelled: preferences.OrderCancelled,
                RefundIssued: preferences.RefundIssued,
                MembershipExpiring: preferences.MembershipExpiring,
                MembershipCancelled: preferences.MembershipCancelled,
                TierUpgrade: preferences.TierUpgrade,
                Promo: preferences.Promo,
                DisputeReply: preferences.DisputeReply,
                RecurringScheduled: preferences.RecurringScheduled));
        }
    }
}
