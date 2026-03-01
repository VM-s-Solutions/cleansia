namespace Cleansia.Core.AppServices.Features.DataRetention;

public static class RetentionDefaults
{
    // Feature flag name
    public const string FeatureFlagName = "DataRetentionJobEnabled";

    // TenantConfiguration keys
    public const string ExpiredCodesEnabledKey = "retention.expired_codes.enabled";
    public const string StaleDevicesDaysKey = "retention.stale_devices.days";
    public const string GdprRequestsYearsKey = "retention.gdpr_requests.years";
    public const string OrderPiiYearsKey = "retention.order_pii.years";
    public const string WithdrawnConsentsYearsKey = "retention.withdrawn_consents.years";
    public const string DeletedDocumentsDaysKey = "retention.deleted_documents.days";

    // Default values
    public const bool DefaultExpiredCodesEnabled = true;
    public const int DefaultStaleDevicesDays = 90;
    public const int DefaultGdprRequestsYears = 3;
    public const int DefaultOrderPiiYears = 2;
    public const int DefaultWithdrawnConsentsYears = 3;
    public const int DefaultDeletedDocumentsDays = 365;

    // Batch size for paged cleanup operations
    public const int BatchSize = 100;
}
