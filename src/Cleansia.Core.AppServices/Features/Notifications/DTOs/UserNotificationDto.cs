namespace Cleansia.Core.AppServices.Features.Notifications.DTOs;

/// <summary>
/// A feed row as the mobile clients consume it: loc-key + args only — no server-rendered
/// title/body/deepLink. Clients render text from their bundled event templates in the device
/// locale and derive the deep link from <see cref="EventKey"/> + <see cref="Args"/>.
/// </summary>
public record UserNotificationDto(
    string Id,
    string EventKey,
    IDictionary<string, string> Args,
    DateTimeOffset CreatedOn,
    DateTimeOffset? ReadOn);
