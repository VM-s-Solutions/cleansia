package cz.cleansia.partner.features.dashboard.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.dashboard.CompletionTimeEfficiency
import cz.cleansia.partner.domain.models.dashboard.EarningsAnalytics
import cz.cleansia.partner.domain.models.dashboard.EarningsDataPoint
import cz.cleansia.partner.domain.models.dashboard.MonthlyEarningsTrend
import cz.cleansia.partner.domain.models.dashboard.OrderStatusDistribution
import cz.cleansia.partner.domain.models.dashboard.PerformanceScore
import cz.cleansia.partner.domain.models.dashboard.ScheduleUtilization
import cz.cleansia.partner.domain.models.dashboard.ServiceRevenueBreakdown
import cz.cleansia.partner.domain.repositories.DashboardRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.LocalDate
import java.time.format.DateTimeFormatter
import javax.inject.Inject

enum class AnalyticsPeriod {
    THIS_WEEK, THIS_MONTH, LAST_MONTH, CUSTOM
}

data class DayOfWeekEarnings(
    val dayIndex: Int, // 1=Monday .. 7=Sunday
    val totalAmount: Double
)

data class AnalyticsUiState(
    val isLoading: Boolean = false,
    val error: String? = null,
    val analytics: EarningsAnalytics? = null,
    val selectedPeriod: AnalyticsPeriod = AnalyticsPeriod.THIS_MONTH,
    val startDate: String? = null,
    val endDate: String? = null,
    val averageDaily: Double = 0.0,
    val bestDay: EarningsDataPoint? = null,
    val worstDay: EarningsDataPoint? = null,
    val dayOfWeekEarnings: List<DayOfWeekEarnings> = emptyList(),
    val orderStatusDistribution: OrderStatusDistribution? = null,
    val performanceScore: PerformanceScore? = null,
    val monthlyEarningsTrend: MonthlyEarningsTrend? = null,
    val serviceRevenueBreakdown: ServiceRevenueBreakdown? = null,
    val scheduleUtilization: ScheduleUtilization? = null,
    val completionTimeEfficiency: CompletionTimeEfficiency? = null
)

@HiltViewModel
class AnalyticsViewModel @Inject constructor(
    private val dashboardRepository: DashboardRepository,
    private val errorTranslator: ApiErrorTranslator
) : ViewModel() {

    private val _uiState = MutableStateFlow(AnalyticsUiState())
    val uiState: StateFlow<AnalyticsUiState> = _uiState.asStateFlow()

    private val dateFormatter = DateTimeFormatter.ofPattern("yyyy-MM-dd")

    init {
        selectPeriod(AnalyticsPeriod.THIS_MONTH)
    }

    fun selectPeriod(period: AnalyticsPeriod) {
        val today = LocalDate.now()
        val (start, end) = when (period) {
            AnalyticsPeriod.THIS_WEEK -> {
                val startOfWeek = today.minusDays(today.dayOfWeek.value.toLong() - 1)
                startOfWeek to today
            }
            AnalyticsPeriod.THIS_MONTH -> {
                today.withDayOfMonth(1) to today
            }
            AnalyticsPeriod.LAST_MONTH -> {
                val lastMonth = today.minusMonths(1)
                lastMonth.withDayOfMonth(1) to lastMonth.withDayOfMonth(lastMonth.lengthOfMonth())
            }
            AnalyticsPeriod.CUSTOM -> {
                // Keep existing dates for custom
                return
            }
        }

        _uiState.update {
            it.copy(
                selectedPeriod = period,
                startDate = start.format(dateFormatter),
                endDate = end.format(dateFormatter)
            )
        }
        loadAnalytics()
    }

    fun setCustomDateRange(startDate: String, endDate: String) {
        _uiState.update {
            it.copy(
                selectedPeriod = AnalyticsPeriod.CUSTOM,
                startDate = startDate,
                endDate = endDate
            )
        }
        loadAnalytics()
    }

    fun clearError() {
        _uiState.update { it.copy(error = null) }
    }

    private fun loadAnalytics() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }

            // Load all data concurrently
            val earningsDeferred = async {
                dashboardRepository.getEarningsAnalytics(
                    startDate = _uiState.value.startDate,
                    endDate = _uiState.value.endDate
                )
            }
            val statusDeferred = async { dashboardRepository.getOrderStatusDistribution() }
            val performanceDeferred = async { dashboardRepository.getPerformanceScore() }
            val trendDeferred = async { dashboardRepository.getMonthlyEarningsTrend() }
            val revenueDeferred = async { dashboardRepository.getServiceRevenueBreakdown() }
            val utilizationDeferred = async { dashboardRepository.getScheduleUtilization() }
            val efficiencyDeferred = async { dashboardRepository.getCompletionTimeEfficiency() }

            // Process earnings analytics (existing)
            when (val result = earningsDeferred.await()) {
                is ApiResult.Success -> {
                    val analytics = result.data
                    val dataPoints = analytics.dataPoints
                    val averageDaily = if (dataPoints.isNotEmpty()) {
                        analytics.totalEarnings / dataPoints.size
                    } else 0.0
                    val bestDay = dataPoints.maxByOrNull { it.amount }
                    val worstDay = dataPoints.filter { it.amount > 0 }.minByOrNull { it.amount }

                    val dayOfWeekMap = mutableMapOf<Int, Double>()
                    for (dp in dataPoints) {
                        try {
                            val date = LocalDate.parse(dp.date, dateFormatter)
                            val dow = date.dayOfWeek.value
                            dayOfWeekMap[dow] = (dayOfWeekMap[dow] ?: 0.0) + dp.amount
                        } catch (_: Exception) { }
                    }
                    val dayOfWeekEarnings = (1..7).map { day ->
                        DayOfWeekEarnings(dayIndex = day, totalAmount = dayOfWeekMap[day] ?: 0.0)
                    }

                    _uiState.update {
                        it.copy(
                            analytics = analytics,
                            averageDaily = averageDaily,
                            bestDay = bestDay,
                            worstDay = worstDay,
                            dayOfWeekEarnings = dayOfWeekEarnings
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(error = errorTranslator.translateError(result.error)) }
                }
            }

            // Process new analytics data
            when (val result = statusDeferred.await()) {
                is ApiResult.Success -> _uiState.update { it.copy(orderStatusDistribution = result.data) }
                is ApiResult.Error -> { }
            }
            when (val result = performanceDeferred.await()) {
                is ApiResult.Success -> _uiState.update { it.copy(performanceScore = result.data) }
                is ApiResult.Error -> { }
            }
            when (val result = trendDeferred.await()) {
                is ApiResult.Success -> _uiState.update { it.copy(monthlyEarningsTrend = result.data) }
                is ApiResult.Error -> { }
            }
            when (val result = revenueDeferred.await()) {
                is ApiResult.Success -> _uiState.update { it.copy(serviceRevenueBreakdown = result.data) }
                is ApiResult.Error -> { }
            }
            when (val result = utilizationDeferred.await()) {
                is ApiResult.Success -> _uiState.update { it.copy(scheduleUtilization = result.data) }
                is ApiResult.Error -> { }
            }
            when (val result = efficiencyDeferred.await()) {
                is ApiResult.Success -> _uiState.update { it.copy(completionTimeEfficiency = result.data) }
                is ApiResult.Error -> { }
            }

            _uiState.update { it.copy(isLoading = false) }
        }
    }
}
