package cz.cleansia.partner.domain.models.dashboard

import kotlinx.serialization.Serializable

@Serializable
data class DashboardStats(
    val availableOrders: Int = 0,
    val myActiveOrders: Int = 0,
    val completedThisMonth: Int = 0,
    val completedLastMonth: Int = 0,
    val pendingEarnings: Double = 0.0,
    val currency: String = "CZK"
) {
    /**
     * Calculates the percentage change between this month and last month completions
     */
    val completionTrend: TrendData
        get() {
            if (completedLastMonth == 0) {
                return TrendData(
                    percentage = if (completedThisMonth > 0) 100.0 else 0.0,
                    direction = if (completedThisMonth > 0) TrendDirection.UP else TrendDirection.NEUTRAL
                )
            }
            val change = ((completedThisMonth - completedLastMonth).toDouble() / completedLastMonth) * 100
            return TrendData(
                percentage = kotlin.math.abs(change),
                direction = when {
                    change > 0 -> TrendDirection.UP
                    change < 0 -> TrendDirection.DOWN
                    else -> TrendDirection.NEUTRAL
                }
            )
        }
}

data class TrendData(
    val percentage: Double = 0.0,
    val direction: TrendDirection = TrendDirection.NEUTRAL
)

enum class TrendDirection {
    UP, DOWN, NEUTRAL
}

@Serializable
data class EarningsAnalytics(
    val totalEarnings: Double = 0.0,
    val previousPeriodEarnings: Double = 0.0,
    val percentageChange: Double = 0.0,
    val currency: String = "CZK",
    val dataPoints: List<EarningsDataPoint> = emptyList()
)

@Serializable
data class EarningsDataPoint(
    val date: String,
    val amount: Double,
    val label: String? = null
)

@Serializable
data class EarningsSummary(
    val thisWeek: Double = 0.0,
    val thisMonth: Double = 0.0,
    val lastMonth: Double = 0.0,
    val currency: String = "CZK"
)

@Serializable
data class UpcomingOrder(
    val id: String,
    val orderNumber: String? = null,
    val scheduledDate: String,
    val scheduledTime: String? = null,
    val customerName: String? = null,
    val address: String? = null,
    val city: String? = null,
    val totalAmount: Double = 0.0,
    val currency: String = "CZK",
    val status: String = "Created",
    val servicesPreview: String? = null
)
