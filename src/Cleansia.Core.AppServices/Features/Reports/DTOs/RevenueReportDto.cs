namespace Cleansia.Core.AppServices.Features.Reports.DTOs;

/// <summary>
/// Completed and paid orders, by completion date, in one currency, minus every refund on those orders.
/// An order counts in the period it was completed in; its refunds are subtracted from it whatever
/// their date, so a refund reduces the month the order completed in, not the month it was issued.
/// Cancelled and unpaid orders are not revenue. A lost chargeback is not subtracted: the platform
/// records the dispute's outcome, not the amount the bank reversed.
/// </summary>
/// <param name="TotalRevenue">
/// GROSS: Σ <c>TotalPrice</c> of the period's completed, paid orders. Stays gross because the by-tender
/// table reconciles against a gateway statement, and a statement shows charges and refunds as separate
/// lines. The ruling's number is <see cref="NetRevenue"/>.
/// </param>
/// <param name="AverageOrderValue">Net revenue over <paramref name="TotalOrders"/>.</param>
/// <param name="TotalOrders">The period's completed, paid orders — a fully refunded one included.</param>
/// <param name="CompletedOrders">Equal to <paramref name="TotalOrders"/> by construction; kept for the wire.</param>
/// <param name="CancelledOrders">
/// Cancelled BOOKINGS in the period on their own axis (<c>CancelledAt</c>), same currency. Not part
/// of revenue; an abandoned card checkout is not a booking and is not counted.
/// </param>
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
    string? CurrencyCode = null,
    /// <summary>
    /// Σ succeeded card refunds of the period's orders, any refund date. The card leg only.
    /// </summary>
    decimal TotalRefundedToCard = 0m,
    /// <summary>
    /// Σ credit returned to customers' balances by those refunds — the credit leg, which the card
    /// never took and so never gave back. Without it a fully refunded order settled partly from
    /// credit would keep the credit share as revenue.
    /// </summary>
    decimal TotalReturnedToCredit = 0m)
{
    /// <summary>
    /// Both legs. DERIVED, never a third summed figure, for the same reason as
    /// <see cref="RevenueByPaymentType.SettledOnTender"/>.
    /// </summary>
    public decimal TotalRefunded => TotalRefundedToCard + TotalReturnedToCredit;

    /// <summary>The headline: the sales, minus every refund on them.</summary>
    public decimal NetRevenue => TotalRevenue - TotalRefunded;
}

/// <param name="Date">The completion date, not the booking date.</param>
/// <param name="Amount">NET of the day's completed orders' refunds, whatever their date.</param>
/// <param name="Refunded">Both refund legs of the day's orders.</param>
public record DailyRevenue(
    DateOnly Date,
    decimal Amount,
    int OrderCount,
    decimal Refunded = 0m);

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
/// <param name="RefundedToCard">Σ succeeded card refunds on this tender's orders.</param>
/// <param name="ReturnedToCredit">Σ credit returned by refunds on this tender's orders.</param>
public record RevenueByPaymentType(
    string PaymentTypeCode,
    string PaymentTypeName,
    decimal TotalRevenue,
    int OrderCount,
    decimal SettledFromCredit,
    decimal RefundedToCard = 0m,
    decimal ReturnedToCredit = 0m)
{
    /// <summary>
    /// What the tender actually took. Without it the Card row claimed the gateway had taken the whole
    /// sale, and a month checked against Stripe came up short by exactly the credit total with nothing
    /// on the screen to explain the gap.
    ///
    /// <para>DERIVED, not a third number the handler computes. The two summed columns and this one
    /// have to close or the row invites the reader to reconcile a rounding error, and the only way to
    /// guarantee that is to not give anyone the chance to compute it twice — the same reasoning as
    /// <c>RefundService.CardChargedAmount</c>.</para>
    /// </summary>
    public decimal SettledOnTender => TotalRevenue - SettledFromCredit;

    /// <summary>
    /// <b>The figure that reconciles against a Stripe statement</b>: what the tender took, less what it
    /// gave back. The card leg only — the gateway never saw the credit, which is shown beside it and
    /// netted only in the headline.
    /// </summary>
    public decimal NetOnTender => SettledOnTender - RefundedToCard;
}

public record RevenueByPaymentStatus(
    string PaymentStatusCode,
    string PaymentStatusName,
    decimal TotalRevenue,
    int OrderCount);
