package cz.cleansia.partner.features.dashboard.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.dashboard.DashboardStats
import cz.cleansia.partner.domain.models.dashboard.EarningsSummary
import cz.cleansia.partner.domain.models.dashboard.UpcomingOrder
import cz.cleansia.partner.domain.models.profile.AvailabilityUtils
import cz.cleansia.partner.domain.models.profile.DayAvailability
import cz.cleansia.partner.domain.models.profile.TimeSlot
import cz.cleansia.partner.domain.models.profile.TodayWorkingInfo
import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.domain.repositories.DashboardRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.util.Calendar
import javax.inject.Inject

data class DashboardUiState(
    val isLoading: Boolean = false,
    val isRefreshing: Boolean = false,
    val error: String? = null,
    val stats: DashboardStats? = null,
    val upcomingOrders: List<UpcomingOrder> = emptyList(),
    val earnings: EarningsSummary? = null,
    val greeting: GreetingType = GreetingType.MORNING,
    val userName: String = "",
    val todayWorkingInfo: TodayWorkingInfo? = null
)

enum class GreetingType {
    MORNING, AFTERNOON, EVENING
}

@HiltViewModel
class DashboardViewModel @Inject constructor(
    private val dashboardRepository: DashboardRepository,
    private val tokenManager: TokenManager,
    private val errorTranslator: ApiErrorTranslator
) : ViewModel() {

    private val _uiState = MutableStateFlow(DashboardUiState())
    val uiState: StateFlow<DashboardUiState> = _uiState.asStateFlow()

    init {
        observeUserName()
        updateGreeting()
        loadDashboardData()
        loadTodayWorkingInfo()
    }

    private fun observeUserName() {
        viewModelScope.launch {
            tokenManager.userFullName.collectLatest { fullName ->
                val firstName = fullName.split(" ").firstOrNull()?.takeIf { it.isNotBlank() } ?: ""
                _uiState.update { it.copy(userName = firstName) }
            }
        }
    }

    private fun updateGreeting() {
        val hour = Calendar.getInstance().get(Calendar.HOUR_OF_DAY)
        val greeting = when {
            hour < 12 -> GreetingType.MORNING
            hour < 17 -> GreetingType.AFTERNOON
            else -> GreetingType.EVENING
        }
        _uiState.update { it.copy(greeting = greeting) }
    }

    fun loadDashboardData() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }

            // Load all data in parallel for faster loading
            val statsDeferred = async { dashboardRepository.getDashboardStats() }
            val ordersDeferred = async { dashboardRepository.getUpcomingOrders() }
            val earningsDeferred = async { dashboardRepository.getEarnings() }

            val statsResult = statsDeferred.await()
            val ordersResult = ordersDeferred.await()
            val earningsResult = earningsDeferred.await()

            val errors = mutableListOf<String>()

            when (statsResult) {
                is ApiResult.Success -> _uiState.update { it.copy(stats = statsResult.data) }
                is ApiResult.Error -> errors.add(errorTranslator.translateError(statsResult.error))
            }

            when (ordersResult) {
                is ApiResult.Success -> _uiState.update { it.copy(upcomingOrders = ordersResult.data) }
                is ApiResult.Error -> errors.add(errorTranslator.translateError(ordersResult.error))
            }

            when (earningsResult) {
                is ApiResult.Success -> _uiState.update { it.copy(earnings = earningsResult.data) }
                is ApiResult.Error -> errors.add(errorTranslator.translateError(earningsResult.error))
            }

            _uiState.update {
                it.copy(
                    isLoading = false,
                    error = if (errors.isNotEmpty() && it.stats == null) errors.first() else null
                )
            }
        }
    }

    fun refresh() {
        viewModelScope.launch {
            _uiState.update { it.copy(isRefreshing = true, error = null) }

            updateGreeting()

            // Load all data in parallel
            val statsDeferred = async { dashboardRepository.getDashboardStats() }
            val ordersDeferred = async { dashboardRepository.getUpcomingOrders() }
            val earningsDeferred = async { dashboardRepository.getEarnings() }

            val statsResult = statsDeferred.await()
            val ordersResult = ordersDeferred.await()
            val earningsResult = earningsDeferred.await()

            when (statsResult) {
                is ApiResult.Success -> _uiState.update { it.copy(stats = statsResult.data) }
                is ApiResult.Error -> _uiState.update { it.copy(error = errorTranslator.translateError(statsResult.error)) }
            }

            when (ordersResult) {
                is ApiResult.Success -> _uiState.update { it.copy(upcomingOrders = ordersResult.data) }
                is ApiResult.Error -> { }
            }

            when (earningsResult) {
                is ApiResult.Success -> _uiState.update { it.copy(earnings = earningsResult.data) }
                is ApiResult.Error -> { }
            }

            loadTodayWorkingInfo()
            _uiState.update { it.copy(isRefreshing = false) }
        }
    }

    private fun loadTodayWorkingInfo() {
        // TODO: Load from ProfileRepository when API endpoint is available
        // For now, compute from mock availability data
        val mockSchedule = listOf(
            DayAvailability(Calendar.MONDAY, true, listOf(TimeSlot("09:00", "17:00"))),
            DayAvailability(Calendar.TUESDAY, true, listOf(TimeSlot("09:00", "17:00"))),
            DayAvailability(Calendar.WEDNESDAY, true, listOf(TimeSlot("09:00", "17:00"))),
            DayAvailability(Calendar.THURSDAY, true, listOf(TimeSlot("09:00", "17:00"))),
            DayAvailability(Calendar.FRIDAY, true, listOf(TimeSlot("09:00", "17:00"))),
            DayAvailability(Calendar.SATURDAY, false),
            DayAvailability(Calendar.SUNDAY, false)
        )
        val info = AvailabilityUtils.getTodayWorkingInfo(mockSchedule, emptyList())
        _uiState.update { it.copy(todayWorkingInfo = info) }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null) }
    }
}
