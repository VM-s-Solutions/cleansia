namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// Queue message dispatched by domain handlers (TakeOrder, CompleteOrder,
/// AddDisputeMessage, …) post-commit to fan out push notifications for a
/// single event addressed to a single user.
///
/// <see cref="EventKey"/> matches the mobile <c>strings.xml</c> lookup
/// (e.g. <c>order.confirmed</c>). <see cref="Args"/> carries structured
/// values to substitute into the localized template (e.g. orderId,
/// orderNumber). NEVER include PII (customer name, address) — payload is
/// visible on the device's lock screen.
///
/// <see cref="TenantId"/> is sent so the consumer Function can set the
/// tenant override before reading the user's <c>UserNotificationPreferences</c>
/// + <c>Device</c> rows (mirrors the GenerateReceipt cross-tenant pattern).
/// </summary>
public record SendPushNotificationMessage(
    string UserId,
    string EventKey,
    Dictionary<string, string> Args,
    string? TenantId);
