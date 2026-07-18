using System.Text.Json;
using Cleansia.Core.AppServices.Features.Notifications.DTOs;
using Cleansia.Core.Domain.Notifications;

namespace Cleansia.Core.AppServices.Features.Notifications.Mappers;

public static class UserNotificationMapper
{
    public static UserNotificationDto MapToDto(this UserNotification notification)
    {
        return new UserNotificationDto(
            notification.Id,
            notification.EventKey,
            JsonSerializer.Deserialize<Dictionary<string, string>>(notification.ArgsJson)
                ?? new Dictionary<string, string>(),
            notification.CreatedOn,
            notification.ReadOn);
    }
}
