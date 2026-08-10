package cz.cleansia.partner.data.dashboard

import cz.cleansia.partner.api.model.AvailableJobsPreviewResponse
import cz.cleansia.partner.api.model.DashboardStatsDto

/**
 * Every member non-zero and non-default. A relaxed mock would satisfy the types and hand every mapper
 * a zero, which is the exact defect the mappers exist to refuse.
 */
internal fun dashboardStatsDto() = DashboardStatsDto(
    availableOrdersCount = 7,
    myActiveOrdersCount = 3,
    thisMonthCompletedOrders = 12,
    lastMonthCompletedOrders = 9,
    todayEarnings = 640.25,
    todayCompletedCount = 2,
    weekEarnings = 4820.75,
    weekCompletedCount = 6,
    lastMonthEarnings = 18240.40,
    currentPeriodEarnings = 9315.60,
    currentPayPeriodStart = "2026-08-01T00:00:00Z",
    currentPayPeriodEnd = "2026-08-15T23:59:59Z",
    nextPayoutDate = "2026-08-20T00:00:00Z",
    averageRating = 4.7,
    ratingCount = 23,
    latestInvoiceStatus = "Approved",
    currencyCode = "CZK",
)

internal fun dashboardStats() = dashboardStatsDto().toDomain()

internal fun availableJobsPreviewResponse() = AvailableJobsPreviewResponse(
    jobs = emptyList(),
    totalPotentialEarnings = 12480.75,
    totalAvailableCount = 7,
)
