package cz.cleansia.partner.domain.repositories

import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.ApiService
import cz.cleansia.partner.core.network.safeApiCall
import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.domain.models.dashboard.CompletionTimeEfficiency
import cz.cleansia.partner.domain.models.dashboard.DashboardStats
import cz.cleansia.partner.domain.models.dashboard.EarningsAnalytics
import cz.cleansia.partner.domain.models.dashboard.EarningsSummary
import cz.cleansia.partner.domain.models.dashboard.MonthlyEarning
import cz.cleansia.partner.domain.models.dashboard.MonthlyEarningsTrend
import cz.cleansia.partner.domain.models.dashboard.OrderStatusDistribution
import cz.cleansia.partner.domain.models.dashboard.PerformanceScore
import cz.cleansia.partner.domain.models.dashboard.ScheduleUtilization
import cz.cleansia.partner.domain.models.dashboard.ServiceRevenue
import cz.cleansia.partner.domain.models.dashboard.ServiceRevenueBreakdown
import cz.cleansia.partner.domain.models.dashboard.ServiceTimeComparison
import cz.cleansia.partner.domain.models.dashboard.UpcomingOrder
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

interface DashboardRepository {
    suspend fun getDashboardStats(): ApiResult<DashboardStats>
    suspend fun getEarningsAnalytics(startDate: String? = null, endDate: String? = null): ApiResult<EarningsAnalytics>
    suspend fun getEarnings(): ApiResult<EarningsSummary>
    suspend fun getUpcomingOrders(limit: Int = 5): ApiResult<List<UpcomingOrder>>
    suspend fun getOrderStatusDistribution(): ApiResult<OrderStatusDistribution>
    suspend fun getPerformanceScore(): ApiResult<PerformanceScore>
    suspend fun getMonthlyEarningsTrend(): ApiResult<MonthlyEarningsTrend>
    suspend fun getServiceRevenueBreakdown(): ApiResult<ServiceRevenueBreakdown>
    suspend fun getScheduleUtilization(): ApiResult<ScheduleUtilization>
    suspend fun getCompletionTimeEfficiency(): ApiResult<CompletionTimeEfficiency>
}

@Singleton
class DashboardRepositoryImpl @Inject constructor(
    private val apiService: ApiService,
    private val tokenManager: TokenManager,
    private val json: Json
) : DashboardRepository {

    override suspend fun getDashboardStats(): ApiResult<DashboardStats> {
        return safeApiCall(json) {
            apiService.getDashboardStats(employeeId = tokenManager.getUserId())
        }
    }

    override suspend fun getEarningsAnalytics(
        startDate: String?,
        endDate: String?
    ): ApiResult<EarningsAnalytics> {
        return safeApiCall(json) {
            apiService.getEarningsAnalytics(
                employeeId = tokenManager.getUserId(),
                startDate = startDate,
                endDate = endDate
            )
        }
    }

    override suspend fun getUpcomingOrders(limit: Int): ApiResult<List<UpcomingOrder>> {
        return safeApiCall(json) {
            apiService.getUpcomingOrders(
                employeeId = tokenManager.getUserId(),
                limit = limit
            )
        }
    }

    override suspend fun getEarnings(): ApiResult<EarningsSummary> {
        return safeApiCall(json) {
            apiService.getEarningsSummary(employeeId = tokenManager.getUserId())
        }
    }

    override suspend fun getOrderStatusDistribution(): ApiResult<OrderStatusDistribution> {
        return when (val result = safeApiCall(json) {
            apiService.getOrderAnalytics(employeeId = tokenManager.getUserId())
        }) {
            is ApiResult.Success -> {
                val data = result.data
                val dist = data.statusDistribution
                ApiResult.Success(
                    OrderStatusDistribution(
                        completed = dist["Completed"] ?: dist["4"] ?: 0,
                        inProgress = dist["InProgress"] ?: dist["3"] ?: 0,
                        cancelled = dist["Cancelled"] ?: dist["5"] ?: 0,
                        pending = dist["Created"] ?: dist["1"] ?: 0
                    )
                )
            }
            is ApiResult.Error -> ApiResult.Error(result.error)
        }
    }

    override suspend fun getPerformanceScore(): ApiResult<PerformanceScore> {
        return when (val result = safeApiCall(json) {
            apiService.getProductivityMetrics(employeeId = tokenManager.getUserId())
        }) {
            is ApiResult.Success -> {
                val data = result.data
                ApiResult.Success(
                    PerformanceScore(
                        overallScore = data.efficiencyScore.toFloat(),
                        customerRating = 0f, // Not available from this endpoint
                        onTimePercentage = data.onTimeCompletionRate.toFloat(),
                        avgResponseMinutes = data.averageCompletionTimeMinutes.toInt()
                    )
                )
            }
            is ApiResult.Error -> ApiResult.Error(result.error)
        }
    }

    override suspend fun getMonthlyEarningsTrend(): ApiResult<MonthlyEarningsTrend> {
        return when (val result = safeApiCall(json) {
            apiService.getEarningsAnalytics(employeeId = tokenManager.getUserId())
        }) {
            is ApiResult.Success -> {
                val analytics = result.data
                // The EarningsAnalytics response includes monthly data in dataPoints
                // Group by month for trend
                val monthlyMap = mutableMapOf<String, Double>()
                for (dp in analytics.dataPoints) {
                    val monthKey = dp.date.take(7) // "YYYY-MM"
                    monthlyMap[monthKey] = (monthlyMap[monthKey] ?: 0.0) + dp.amount
                }
                val months = monthlyMap.entries
                    .sortedBy { it.key }
                    .map { MonthlyEarning(month = it.key, amount = it.value) }
                val totalThisYear = months.sumOf { it.amount }
                val change = if (months.size >= 2) {
                    val last = months[months.size - 1].amount
                    val prev = months[months.size - 2].amount
                    if (prev > 0) ((last - prev) / prev * 100).toFloat() else 0f
                } else 0f

                ApiResult.Success(
                    MonthlyEarningsTrend(
                        months = months,
                        totalThisYear = totalThisYear,
                        monthOverMonthChange = change
                    )
                )
            }
            is ApiResult.Error -> ApiResult.Error(result.error)
        }
    }

    override suspend fun getServiceRevenueBreakdown(): ApiResult<ServiceRevenueBreakdown> {
        return when (val result = safeApiCall(json) {
            apiService.getOrderAnalytics(employeeId = tokenManager.getUserId())
        }) {
            is ApiResult.Success -> {
                val services = result.data.serviceDistribution.map { svc ->
                    ServiceRevenue(
                        serviceName = svc.serviceName,
                        revenue = svc.totalRevenue,
                        orderCount = svc.orderCount
                    )
                }
                ApiResult.Success(ServiceRevenueBreakdown(services = services))
            }
            is ApiResult.Error -> ApiResult.Error(result.error)
        }
    }

    override suspend fun getScheduleUtilization(): ApiResult<ScheduleUtilization> {
        return when (val result = safeApiCall(json) {
            apiService.getTimeAnalytics(employeeId = tokenManager.getUserId())
        }) {
            is ApiResult.Success -> {
                val data = result.data
                // Estimate: 8 hours/day × days worked
                val daysWorked = data.dailyBreakdown.count { it.actualMinutes > 0 }
                val availableMinutes = (daysWorked * 8 * 60).toFloat()
                val bookedMinutes = data.totalMinutesWorked.toFloat()
                val rate = if (availableMinutes > 0) bookedMinutes / availableMinutes else 0f
                ApiResult.Success(
                    ScheduleUtilization(
                        availableHours = availableMinutes / 60f,
                        bookedHours = bookedMinutes / 60f,
                        utilizationRate = rate.coerceIn(0f, 1f)
                    )
                )
            }
            is ApiResult.Error -> ApiResult.Error(result.error)
        }
    }

    override suspend fun getCompletionTimeEfficiency(): ApiResult<CompletionTimeEfficiency> {
        return when (val result = safeApiCall(json) {
            apiService.getTimeAnalytics(employeeId = tokenManager.getUserId())
        }) {
            is ApiResult.Success -> {
                val services = result.data.byServiceType.map { svc ->
                    ServiceTimeComparison(
                        serviceName = svc.serviceName,
                        estimatedMinutes = svc.averageMinutesPerOrder,
                        actualMinutes = if (svc.orderCount > 0) svc.totalMinutes / svc.orderCount else 0
                    )
                }
                ApiResult.Success(CompletionTimeEfficiency(services = services))
            }
            is ApiResult.Error -> ApiResult.Error(result.error)
        }
    }
}
