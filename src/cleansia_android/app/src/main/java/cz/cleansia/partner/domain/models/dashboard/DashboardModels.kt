package cz.cleansia.partner.domain.models.dashboard

import kotlinx.serialization.Serializable

@Serializable
data class DashboardStats(
    val availableOrders: Int = 0,
    val myActiveOrders: Int = 0,
    val completedThisMonth: Int = 0,
    val completedLastMonth: Int = 0,
    val pendingEarnings: Double = 0.0,
    val currency: String = "EUR"
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
    val currency: String = "EUR",
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
    val currency: String = "EUR"
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
    val currency: String = "EUR",
    val status: String = "Created",
    val servicesPreview: String? = null
)

// Analytics extension models

data class OrderStatusDistribution(
    val completed: Int,
    val inProgress: Int,
    val cancelled: Int,
    val pending: Int
)

data class PerformanceScore(
    val overallScore: Float,
    val customerRating: Float,
    val onTimePercentage: Float,
    val avgResponseMinutes: Int
)

data class MonthlyEarningsTrend(
    val months: List<MonthlyEarning>,
    val totalThisYear: Double,
    val monthOverMonthChange: Float
)

data class MonthlyEarning(
    val month: String,
    val amount: Double
)

data class ServiceRevenueBreakdown(
    val services: List<ServiceRevenue>
)

data class ServiceRevenue(
    val serviceName: String,
    val revenue: Double,
    val orderCount: Int
)

data class ScheduleUtilization(
    val availableHours: Float,
    val bookedHours: Float,
    val utilizationRate: Float
)

data class CompletionTimeEfficiency(
    val services: List<ServiceTimeComparison>
)

data class ServiceTimeComparison(
    val serviceName: String,
    val estimatedMinutes: Int,
    val actualMinutes: Int
)

// ── Backend API response DTOs ──
// These match the .NET backend JSON shapes and are mapped to domain models in the repository.

@Serializable
data class OrderAnalyticsResponse(
    val statusDistribution: Map<String, Int> = emptyMap(),
    val weeklyTrends: List<WeeklyTrendItem> = emptyList(),
    val serviceDistribution: List<ServiceDistributionItem> = emptyList(),
    val totalOrders: Int = 0,
    val completionRate: Double = 0.0,
    val cancelledOrders: Int = 0
)

@Serializable
data class WeeklyTrendItem(
    val year: Int = 0,
    val weekNumber: Int = 0,
    val weekStartDate: String? = null,
    val orderCount: Int = 0,
    val completedCount: Int = 0,
    val totalRevenue: Double = 0.0
)

@Serializable
data class ServiceDistributionItem(
    val serviceName: String = "",
    val orderCount: Int = 0,
    val averagePrice: Double = 0.0,
    val totalRevenue: Double = 0.0
)

@Serializable
data class TimeAnalyticsResponse(
    val dailyBreakdown: List<DailyTimeBreakdown> = emptyList(),
    val weeklyBreakdown: List<WeeklyTimeBreakdown> = emptyList(),
    val byServiceType: List<ServiceTimeBreakdown> = emptyList(),
    val totalMinutesWorked: Int = 0,
    val averageMinutesPerOrder: Int = 0,
    val efficiencyRate: Double = 0.0,
    val totalOrders: Int = 0
)

@Serializable
data class DailyTimeBreakdown(
    val date: String? = null,
    val estimatedMinutes: Int = 0,
    val actualMinutes: Int = 0,
    val ordersCompleted: Int = 0,
    val dayOfWeek: String? = null
)

@Serializable
data class WeeklyTimeBreakdown(
    val year: Int = 0,
    val weekNumber: Int = 0,
    val weekStartDate: String? = null,
    val totalMinutes: Int = 0,
    val ordersCompleted: Int = 0,
    val averageMinutesPerOrder: Int = 0
)

@Serializable
data class ServiceTimeBreakdown(
    val serviceName: String = "",
    val totalMinutes: Int = 0,
    val orderCount: Int = 0,
    val averageMinutesPerOrder: Int = 0
)

@Serializable
data class ProductivityMetricsResponse(
    val ordersCompleted: Int = 0,
    val ordersTarget: Int = 0,
    val completionPercentage: Double = 0.0,
    val averageCompletionTimeMinutes: Double = 0.0,
    val onTimeCompletionRate: Double = 0.0,
    val efficiencyScore: Double = 0.0,
    val personalBests: PersonalBests? = null
)

@Serializable
data class PersonalBests(
    val highestEarningMonth: BackendMonthlyEarning? = null,
    val mostOrdersInDay: Int = 0,
    val mostOrdersDate: String? = null,
    val mostOrdersInMonth: Int = 0,
    val currentMonthYear: Int = 0,
    val bestEfficiencyScore: Double = 0.0
)

@Serializable
data class BackendMonthlyEarning(
    val year: Int = 0,
    val month: Int = 0,
    val amount: Double = 0.0,
    val monthName: String? = null
)
