using Cleansia.Core.AppServices.Features.DataRetention;

namespace Cleansia.Core.AppServices.Features.TenantSettings;

/// <summary>
/// Every key an operating company may hold in <c>TenantConfigurations</c>. A key outside this list is
/// refused by the writer and ignored by the readers, so the table can never carry a value nothing
/// reads. The retention defaults are the <see cref="RetentionDefaults"/> constants the sweeps were
/// written against; the lifecycle entry is the archive's chargeback horizon (ADR-0064 D3).
/// </summary>
public static class TenantSettingCatalog
{
    public const string RetentionCategory = "retention";

    public const string LifecycleCategory = "lifecycle";

    public const string ChargebackHorizonDaysKey = "lifecycle.chargeback_horizon_days";

    // Card networks let a cardholder dispute a charge for 120 days and longer on some reason codes; a
    // chargeback on a sealed company is a books event the freeze would refuse, so the archive waits.
    public const int DefaultChargebackHorizonDays = 180;
    private const int MaxChargebackHorizonDays = 730;

    // DateTimeOffset.AddYears/AddDays throw past the calendar's end, so a window needs a ceiling as
    // well as the floor of one the sweeps enforce; a century is far beyond any retention obligation.
    private const int MaxYears = 100;
    private const int MaxDays = 36_500;

    public static readonly BoolTenantSetting ExpiredCodesEnabled = new(
        RetentionDefaults.ExpiredCodesEnabledKey, RetentionCategory, RetentionDefaults.DefaultExpiredCodesEnabled);

    public static readonly IntTenantSetting StaleDevicesDays = Days(
        RetentionDefaults.StaleDevicesDaysKey, RetentionDefaults.DefaultStaleDevicesDays);

    public static readonly IntTenantSetting GdprRequestsYears = Years(
        RetentionDefaults.GdprRequestsYearsKey, RetentionDefaults.DefaultGdprRequestsYears);

    public static readonly IntTenantSetting OrderPiiYears = Years(
        RetentionDefaults.OrderPiiYearsKey, RetentionDefaults.DefaultOrderPiiYears);

    public static readonly IntTenantSetting WithdrawnConsentsYears = Years(
        RetentionDefaults.WithdrawnConsentsYearsKey, RetentionDefaults.DefaultWithdrawnConsentsYears);

    public static readonly IntTenantSetting DeletedDocumentsDays = Days(
        RetentionDefaults.DeletedDocumentsDaysKey, RetentionDefaults.DefaultDeletedDocumentsDays);

    public static readonly IntTenantSetting NotificationsDays = Days(
        RetentionDefaults.NotificationsDaysKey, RetentionDefaults.DefaultNotificationsDays);

    public static readonly IntTenantSetting CustomerAuditRetentionYears = Years(
        RetentionDefaults.CustomerAuditRetentionYearsKey, RetentionDefaults.DefaultCustomerAuditRetentionYears);

    public static readonly IntTenantSetting DisputeTextRetentionYears = Years(
        RetentionDefaults.DisputeTextRetentionYearsKey, RetentionDefaults.DefaultDisputeTextRetentionYears);

    public static readonly IntTenantSetting ChargebackHorizonDays = new(
        ChargebackHorizonDaysKey, LifecycleCategory, DefaultChargebackHorizonDays, min: 0, max: MaxChargebackHorizonDays);

    public static readonly IReadOnlyList<TenantSettingDefinition> All =
    [
        ExpiredCodesEnabled,
        StaleDevicesDays,
        GdprRequestsYears,
        OrderPiiYears,
        WithdrawnConsentsYears,
        DeletedDocumentsDays,
        NotificationsDays,
        CustomerAuditRetentionYears,
        DisputeTextRetentionYears,
        ChargebackHorizonDays,
    ];

    private static readonly IReadOnlyDictionary<string, TenantSettingDefinition> ByKey =
        All.ToDictionary(definition => definition.Key, StringComparer.Ordinal);

    public static TenantSettingDefinition? Find(string? key) =>
        key is null ? null : ByKey.GetValueOrDefault(key);

    private static IntTenantSetting Years(string key, int @default) =>
        new(key, RetentionCategory, @default, min: 1, max: MaxYears);

    private static IntTenantSetting Days(string key, int @default) =>
        new(key, RetentionCategory, @default, min: 1, max: MaxDays);
}
