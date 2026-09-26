namespace Cleansia.Core.AppServices.Features.DataRetention;

public static class RetentionDefaults
{
    public const string ExpiredCodesEnabledKey = "retention.expired_codes.enabled";
    public const string StaleDevicesDaysKey = "retention.stale_devices.days";
    public const string GdprRequestsYearsKey = "retention.gdpr_requests.years";
    public const string OrderPiiYearsKey = "retention.order_pii.years";
    public const string WithdrawnConsentsYearsKey = "retention.withdrawn_consents.years";
    public const string DeletedDocumentsDaysKey = "retention.deleted_documents.days";
    public const string NotificationsDaysKey = "retention.notifications.days";
    public const string CustomerAuditRetentionYearsKey = "retention.customer_audit.years";
    public const string DisputeTextRetentionYearsKey = "retention.dispute_text.years";
    public const string WorkContractMetadataRetentionYearsKey = "retention.work_contract_metadata.years";
    public const string OrderPhotosDaysKey = "retention.order_photos.days";
    public const string AdminAuditRetentionYearsKey = "retention.admin_audit.years";
    public const string EmployeeAuditRetentionYearsKey = "retention.employee_audit.years";

    public const bool DefaultExpiredCodesEnabled = true;
    public const int DefaultStaleDevicesDays = 90;
    public const int DefaultGdprRequestsYears = 3;
    public const int DefaultOrderPiiYears = 2;
    public const int DefaultWithdrawnConsentsYears = 3;
    public const int DefaultDeletedDocumentsDays = 365;
    public const int DefaultNotificationsDays = 90;
    public const int DefaultCustomerAuditRetentionYears = 3;
    public const int DefaultDisputeTextRetentionYears = 3;
    public const int DefaultWorkContractMetadataRetentionYears = 3;
    public const int DefaultOrderPhotosDays = 7;
    public const int DefaultAdminAuditRetentionYears = 3;
    public const int DefaultEmployeeAuditRetentionYears = 3;

    /// <summary>
    /// Runaway/abuse guard on the notifications feed — an order of magnitude above a realistic
    /// 90-day maximum, so it never eats a legitimate user's unread history.
    /// </summary>
    public const int MaxNotificationsPerUser = 500;

    public const int BatchSize = 100;
}
