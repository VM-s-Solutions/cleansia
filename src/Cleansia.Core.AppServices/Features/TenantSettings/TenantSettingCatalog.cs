using Cleansia.Core.AppServices.Features.DataRetention;

namespace Cleansia.Core.AppServices.Features.TenantSettings;

/// <summary>
/// Every key an operating company may hold in <c>TenantConfigurations</c>. A key outside this list is
/// refused by the writer and ignored by the readers, so the table can never carry a value nothing
/// reads. The retention defaults are the <see cref="RetentionDefaults"/> constants the sweeps were
/// written against; the lifecycle entry is the archive's chargeback horizon (ADR-0064 D3); the
/// notifications entry is the shared mailbox admin events are e-mailed to instead of every
/// administrator (ADR-0065 D3) — empty, its default, means every administrator. The cash entries are the
/// cash a cleaner may hold before cash jobs are hidden from them — 0, its default, is no cap — and the
/// days a balance carried past a pay-period close may last before the cleaner is asked to hand it over
/// (owner ruling 2026-09-28, decisions 23 and 25). The pay entry is the share of the extras' prices a job pays
/// its crew (owner decision 2026-10-04).
/// </summary>
public static class TenantSettingCatalog
{
    public const string RetentionCategory = "retention";

    public const string LifecycleCategory = "lifecycle";

    public const string NotificationsCategory = "notifications";

    public const string CashCategory = "cash";

    public const string PayCategory = "pay";

    public const string ChargebackHorizonDaysKey = "lifecycle.chargeback_horizon_days";

    public const string AdminNotificationEmailKey = "notifications.admin_email";

    public const string CashFloatCapKey = "cash.float_cap";

    public const string CashRemittanceRequestDaysKey = "cash.remittance_request_days";

    public const string ExtrasSharePercentKey = "pay.extras_share_percent";

    public const int DefaultCashRemittanceRequestDays = 30;
    private const int MaxCashFloatCap = 10_000_000;
    private const int MaxCashRemittanceRequestDays = 365;

    public const int DefaultExtrasSharePercent = 50;

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

    public static readonly IntTenantSetting WorkContractMetadataRetentionYears = Years(
        RetentionDefaults.WorkContractMetadataRetentionYearsKey, RetentionDefaults.DefaultWorkContractMetadataRetentionYears);

    public static readonly IntTenantSetting OrderPhotosDays = Days(
        RetentionDefaults.OrderPhotosDaysKey, RetentionDefaults.DefaultOrderPhotosDays);

    public static readonly IntTenantSetting AdminAuditRetentionYears = Years(
        RetentionDefaults.AdminAuditRetentionYearsKey, RetentionDefaults.DefaultAdminAuditRetentionYears);

    public static readonly IntTenantSetting EmployeeAuditRetentionYears = Years(
        RetentionDefaults.EmployeeAuditRetentionYearsKey, RetentionDefaults.DefaultEmployeeAuditRetentionYears);

    // Floored at the statutory period, not the generic one year: the sweep deletes the PDF for good and
    // nothing re-renders it, so a lower figure would destroy tax documents still owed to the authority.
    public static readonly IntTenantSetting ReceiptsYears = new(
        RetentionDefaults.ReceiptsYearsKey, RetentionCategory, RetentionDefaults.DefaultReceiptsYears,
        min: RetentionDefaults.DefaultReceiptsYears, max: MaxYears);

    public static readonly IntTenantSetting ChargebackHorizonDays = new(
        ChargebackHorizonDaysKey, LifecycleCategory, DefaultChargebackHorizonDays, min: 0, max: MaxChargebackHorizonDays);

    public static readonly EmailTenantSetting AdminNotificationEmail = new(
        AdminNotificationEmailKey, NotificationsCategory, @default: string.Empty);

    public static readonly IntTenantSetting CashFloatCap = new(
        CashFloatCapKey, CashCategory, 0, min: 0, max: MaxCashFloatCap);

    public static readonly IntTenantSetting CashRemittanceRequestDays = new(
        CashRemittanceRequestDaysKey, CashCategory, DefaultCashRemittanceRequestDays, min: 1, max: MaxCashRemittanceRequestDays);

    public static readonly IntTenantSetting ExtrasSharePercent = new(
        ExtrasSharePercentKey, PayCategory, DefaultExtrasSharePercent, min: 0, max: 100);

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
        WorkContractMetadataRetentionYears,
        OrderPhotosDays,
        AdminAuditRetentionYears,
        EmployeeAuditRetentionYears,
        ReceiptsYears,
        ChargebackHorizonDays,
        AdminNotificationEmail,
        CashFloatCap,
        CashRemittanceRequestDays,
        ExtrasSharePercent,
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
