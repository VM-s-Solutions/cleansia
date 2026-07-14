using Cleansia.Core.Domain.Notifications;
using FirebaseAdmin.Messaging;

namespace Cleansia.Infra.Clients.Fcm;

/// <summary>
/// Pure translator from <c>(deviceTokens, eventKey, data)</c> to the per-platform FCM wire shape
/// (ADR-0025): the byte-stable data-only payload + <see cref="AndroidConfig"/> both platforms
/// receive today, plus an APNs-scoped <c>aps.alert</c> (derived loc-keys + allowlisted ordered
/// args) iff the event is in <see cref="ApnsDisplayMap"/>. Platform-blind by design — FCM applies
/// the APNs block only on the APNs route, so Android delivery is bit-identical with or without it.
/// </summary>
public static class FcmMessageFactory
{
    private static readonly IReadOnlyList<string> OrderNumberArg = ["orderNumber"];
    private static readonly IReadOnlyList<string> CountArg = ["count"];
    private static readonly IReadOnlyList<string> NoArgs = [];

    /// <summary>
    /// The APNs display map (ADR-0025 D2): event key → ordered loc-arg names, rendered on iOS via
    /// <c>push.&lt;event_key&gt;.title|body</c> from the app's own bundled catalog. A key absent
    /// here ships data-only (invisible on iOS) — drop-parity with Android's unknown-key behavior;
    /// <c>promo.new_sitewide</c> is structurally excluded (no fixed template anywhere). Add a key
    /// ONLY after its loc-keys ship in BOTH iOS apps' main-bundle catalogs (client-first rule),
    /// and keep arg names inside the closed {orderNumber, count} lock-screen allowlist (D3) —
    /// internal ids and raw enum values must never render.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ApnsDisplayMap { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [NotificationEventCatalog.OrderConfirmed] = OrderNumberArg,
            [NotificationEventCatalog.OrderOnTheWay] = OrderNumberArg,
            [NotificationEventCatalog.OrderInProgress] = OrderNumberArg,
            [NotificationEventCatalog.OrderCompleted] = OrderNumberArg,
            [NotificationEventCatalog.OrderCancelled] = OrderNumberArg,
            [NotificationEventCatalog.OrderRefunded] = OrderNumberArg,
            [NotificationEventCatalog.RecurringScheduled] = OrderNumberArg,
            [NotificationEventCatalog.NewJobsAvailable] = CountArg,
            [NotificationEventCatalog.DisputeReply] = NoArgs,
            [NotificationEventCatalog.LoyaltyTierUpgrade] = NoArgs,
            [NotificationEventCatalog.MembershipExpiringSoon] = NoArgs,
            [NotificationEventCatalog.MembershipCancellationEffective] = NoArgs,
        };

    public static MulticastMessage Build(
        IReadOnlyList<string> tokens,
        string eventKey,
        IReadOnlyDictionary<string, string> data)
    {
        var payload = new Dictionary<string, string>(data) { ["event_key"] = eventKey };

        return new MulticastMessage
        {
            Tokens = tokens.ToArray(),
            Data = payload,
            // Android-specific: high-priority so transactional events wake the device immediately.
            Android = new AndroidConfig
            {
                Priority = Priority.High,
            },
            Apns = BuildApns(eventKey, data),
        };
    }

    private static ApnsConfig? BuildApns(string eventKey, IReadOnlyDictionary<string, string> data)
    {
        if (!ApnsDisplayMap.TryGetValue(eventKey, out var argNames))
        {
            return null;
        }

        return new ApnsConfig
        {
            Headers = new Dictionary<string, string> { ["apns-priority"] = "10" },
            Aps = new Aps
            {
                Alert = new ApsAlert
                {
                    TitleLocKey = $"push.{eventKey}.title",
                    LocKey = $"push.{eventKey}.body",
                    // A missing arg substitutes "" — the alert still displays (parity with
                    // Android's .orEmpty()); dropping it would silence the event on iOS.
                    LocArgs = argNames.Select(name => data.GetValueOrDefault(name, string.Empty)).ToList(),
                },
                Sound = "default",
                ThreadId = data.GetValueOrDefault("orderId")
                           ?? data.GetValueOrDefault("disputeId")
                           ?? eventKey,
            },
        };
    }
}
