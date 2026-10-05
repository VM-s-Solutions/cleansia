namespace Cleansia.Core.Domain.Notifications;

/// <summary>
/// The admin feed's event keys — the strings on a <see cref="UserNotification"/> row an administrator
/// reads. Kept beside the two client keysets because the feed audience switch lives here and Domain
/// cannot read the AppServices table that says what each event carries and who is told.
/// → /architecture/push-notifications#event-catalogue
/// </summary>
public static class AdminNotificationEventCatalog
{
    public const string OrderNew = "admin.order.new";
    public const string OrderCrewLost = "admin.order.crew_lost";
    public const string OrderCleanerNotStarted = "admin.order.cleaner_not_started";
    public const string OrderLockoutReported = "admin.order.lockout_reported";
    public const string OrderCashNotPaid = "admin.order.cash_not_paid";
    public const string DisputeFiled = "admin.dispute.filed";
    public const string DisputeChargeback = "admin.dispute.chargeback";
    public const string DisputeChargebackUnmatched = "admin.dispute.chargeback_unmatched";
    public const string PaymentFailed = "admin.payment.failed";
    public const string RefundStuck = "admin.payment.refund_stuck";
    public const string RefundNeedsRetry = "admin.payment.refund_needs_retry";
    public const string ErasureFailed = "admin.erasure.failed";
    public const string CompanyWindDownRequested = "admin.company.wind_down_requested";
    public const string CompanyWindDownRun = "admin.company.wind_down_run";
    public const string CompanyArchived = "admin.company.archived";
    public const string ReferralHeld = "admin.referral.held";

    public static readonly IReadOnlyList<string> All =
    [
        OrderNew,
        OrderCrewLost,
        OrderCleanerNotStarted,
        OrderLockoutReported,
        OrderCashNotPaid,
        DisputeFiled,
        DisputeChargeback,
        DisputeChargebackUnmatched,
        PaymentFailed,
        RefundStuck,
        RefundNeedsRetry,
        ErasureFailed,
        CompanyWindDownRequested,
        CompanyWindDownRun,
        CompanyArchived,
        ReferralHeld,
    ];
}
