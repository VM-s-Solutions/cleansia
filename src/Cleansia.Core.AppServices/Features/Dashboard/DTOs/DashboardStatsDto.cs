namespace Cleansia.Core.AppServices.Features.Dashboard.DTOs;

public record DashboardStatsDto(
    int AvailableOrdersCount,
    int MyActiveOrdersCount,
    int ThisMonthCompletedOrders,
    int LastMonthCompletedOrders,

    // Today / week / last-month / current-pay-period — the four temporal
    // slices the cleaner reads when they open the app.
    decimal TodayEarnings,
    int TodayCompletedCount,
    decimal WeekEarnings,
    int WeekCompletedCount,
    decimal LastMonthEarnings,
    decimal CurrentPeriodEarnings,

    // Drives the dashboard's pay-period progress bar + "Next payout: …" line.
    // Nullable so the field stays valid for accounts not yet attached to an
    // active period.
    DateTime? CurrentPayPeriodStart,
    DateTime? CurrentPayPeriodEnd,
    DateTime? NextPayoutDate,

    // Average across all OrderReviews where the rated order had this
    // employee assigned. Null when the cleaner has never been reviewed.
    double? AverageRating,
    int RatingCount,

    string? LatestInvoiceStatus,

    // ISO code of the employee's preferred currency (e.g. "CZK", "EUR").
    // Clients use this to format any of the money fields on this DTO —
    // device locale alone is wrong (a Czech cleaner on a US-locale
    // emulator would otherwise see "$"). Null only when the employee
    // has no preference set; clients fall back to device locale.
    string? CurrencyCode);
