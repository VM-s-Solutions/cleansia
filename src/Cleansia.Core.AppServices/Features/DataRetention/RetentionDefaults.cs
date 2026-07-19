namespace Cleansia.Core.AppServices.Features.DataRetention;

public static class RetentionDefaults
{
    public const string FeatureFlagName = "DataRetentionJobEnabled";

    public const string ExpiredCodesEnabledKey = "retention.expired_codes.enabled";
    public const string StaleDevicesDaysKey = "retention.stale_devices.days";
    public const string GdprRequestsYearsKey = "retention.gdpr_requests.years";
    public const string OrderPiiYearsKey = "retention.order_pii.years";
    public const string WithdrawnConsentsYearsKey = "retention.withdrawn_consents.years";
    public const string DeletedDocumentsDaysKey = "retention.deleted_documents.days";
    public const string NotificationsDaysKey = "retention.notifications.days";

    public const bool DefaultExpiredCodesEnabled = true;
    public const int DefaultStaleDevicesDays = 90;
    public const int DefaultGdprRequestsYears = 3;
    public const int DefaultOrderPiiYears = 2;
    public const int DefaultWithdrawnConsentsYears = 3;
    public const int DefaultDeletedDocumentsDays = 365;
    public const int DefaultNotificationsDays = 90;

    /// <summary>
    /// Runaway/abuse guard on the notifications feed — an order of magnitude above a realistic
    /// 90-day maximum, so it never eats a legitimate user's unread history.
    /// </summary>
    public const int MaxNotificationsPerUser = 500;

    public const int BatchSize = 100;
}
