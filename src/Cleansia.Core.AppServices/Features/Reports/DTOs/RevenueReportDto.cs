namespace Cleansia.Core.AppServices.Features.Reports.DTOs;

public record RevenueReportDto(
    decimal TotalRevenue,
    decimal AverageOrderValue,
    int TotalOrders,
    int CompletedOrders,
    int CancelledOrders,
    decimal GrowthPercentage,
    IEnumerable<DailyRevenue> DailyRevenues,
    IEnumerable<RevenueByService> RevenueByService,
    IEnumerable<RevenueByPackage> RevenueByPackage,
    IEnumerable<RevenueByPaymentType> RevenueByPaymentType,
    IEnumerable<RevenueByPaymentStatus> RevenueByPaymentStatus,
    /// <summary>
    /// Total settled from customers' credit balances across the period. Revenue the platform earned
    /// but no payment gateway ever moved, because it was money the platform already owed.
    /// </summary>
    decimal TotalSettledFromCredit = 0m,
    /// <summary>
    /// The currency EVERY amount on this report is in. One report, one currency: the admin chose it
    /// (or took the platform default) and the query filtered on it, so a client formats with this
    /// and never with a hardcoded code. Nullable + defaulted so it is additive on the wire.
    /// </summary>
    string? CurrencyCode = null);

public record DailyRevenue(
    DateOnly Date,
    decimal Amount,
    int OrderCount);

public record RevenueByService(
    string ServiceId,
    string ServiceName,
    decimal TotalRevenue,
    int OrderCount);

public record RevenueByPackage(
    string PackageId,
    string PackageName,
    decimal TotalRevenue,
    int OrderCount);

/// <param name="TotalRevenue">
/// The SALE, which is what revenue means. Credit is a tender, not a discount, so an order settled
/// partly from a customer's balance is still a sale of its full size and belongs here in full.
/// </param>
/// <param name="SettledFromCredit">
/// How much of <paramref name="TotalRevenue"/> was settled from customers' credit balances rather
/// than by this tender.
/// </param>
public record RevenueByPaymentType(
    string PaymentTypeCode,
    string PaymentTypeName,
    decimal TotalRevenue,
    int OrderCount,
    decimal SettledFromCredit)
{
    /// <summary>
    /// What the tender actually took. <b>This is the figure that reconciles against a Stripe
    /// statement</b> — without it the Card row claimed the gateway had taken the whole sale, and a
    /// month checked against Stripe came up short by exactly the credit total with nothing on the
    /// screen to explain the gap.
    ///
    /// <para>DERIVED, not a third number the handler computes. The two summed columns and this one
    /// have to close or the row invites the reader to reconcile a rounding error, and the only way to
    /// guarantee that is to not give anyone the chance to compute it twice — the same reasoning as
    /// <c>RefundService.CardChargedAmount</c>.</para>
    /// </summary>
    public decimal SettledOnTender => TotalRevenue - SettledFromCredit;
}

public record RevenueByPaymentStatus(
    string PaymentStatusCode,
    string PaymentStatusName,
    decimal TotalRevenue,
    int OrderCount);