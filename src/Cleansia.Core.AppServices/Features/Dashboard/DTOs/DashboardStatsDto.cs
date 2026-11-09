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

    // Drives the dashboard's pay-period progress bar. Nullable so the field
    // stays valid for accounts not yet attached to an active period.
    DateOnly? CurrentPayPeriodStart,
    DateOnly? CurrentPayPeriodEnd,

    // Average across all OrderReviews where the rated order had this
    // employee assigned. Null when the cleaner has never been reviewed.
    double? AverageRating,
    int RatingCount,

    string? LatestInvoiceStatus,

    // ISO code of the currency the cleaner is paid in, from their work country (e.g. "CZK", "EUR").
    // Clients use this to format any of the money fields on this DTO —
    // device locale alone is wrong (a Czech cleaner on a US-locale
    // emulator would otherwise see "$"). The server always sends one: the
    // currency resolution throws rather than return none. Should a client
    // still get none, it shows the bare number, never a guessed symbol.
    // → /partner-app/dashboard#currency-formatting
    string? CurrencyCode);
