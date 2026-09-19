using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Notifications;

namespace Cleansia.Core.AppServices.Features.AdminNotifications;

/// <summary>
/// What each admin event carries and who is told, keyed by <see cref="AdminNotificationEventCatalog"/>.
/// <see cref="Entry.EmailArgOrder"/> is the exact set of arg names a site passes — ids, numbers,
/// enum names, dates and money, never a person — in the order the e-mail copy substitutes them.
/// <see cref="Entry.Audience"/> is the administrator set the recipients narrow to; every entry is the
/// whole company until roles exist.
/// </summary>
public static class AdminEventCatalog
{
    public sealed record Entry(string Key, string Audience, IReadOnlyList<string> EmailArgOrder);

    public static readonly IReadOnlyList<Entry> All =
    [
        new(AdminNotificationEventCatalog.OrderNew, PhysicalPolicy.AdminOnly,
            ["orderNumber", "amount", "paymentType", "countryId", "orderId"]),
        new(AdminNotificationEventCatalog.OrderCrewLost, PhysicalPolicy.AdminOnly,
            ["orderNumber", "cause", "statusAtLoss", "cleaningDateTime", "orderId"]),
        new(AdminNotificationEventCatalog.DisputeFiled, PhysicalPolicy.AdminOnly,
            ["orderNumber", "reason", "disputeId", "orderId"]),
        new(AdminNotificationEventCatalog.DisputeChargeback, PhysicalPolicy.AdminOnly,
            ["orderNumber", "amount", "disputeId", "orderId"]),
        new(AdminNotificationEventCatalog.PaymentFailed, PhysicalPolicy.AdminOnly,
            ["orderNumber", "orderId"]),
        new(AdminNotificationEventCatalog.ErasureFailed, PhysicalPolicy.AdminOnly,
            ["day", "requestId"]),
        new(AdminNotificationEventCatalog.CompanyWindDownRequested, PhysicalPolicy.AdminOnly,
            ["windDownFrom"]),
        new(AdminNotificationEventCatalog.CompanyWindDownRun, PhysicalPolicy.AdminOnly,
            ["cancelled", "refunded", "refundFailures", "periodsClosed"]),
        new(AdminNotificationEventCatalog.CompanyArchived, PhysicalPolicy.AdminOnly,
            ["archivedOn"]),
    ];

    private static readonly IReadOnlyDictionary<string, Entry> ByKey =
        All.ToDictionary(e => e.Key, StringComparer.Ordinal);

    public static Entry Find(string key) =>
        ByKey.TryGetValue(key, out var entry)
            ? entry
            : throw new ArgumentOutOfRangeException(nameof(key), key, "Not an admin event key.");
}
