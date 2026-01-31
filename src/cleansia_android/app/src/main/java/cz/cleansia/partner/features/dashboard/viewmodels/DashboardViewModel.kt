package cz.cleansia.partner.features.dashboard.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.dashboard.DashboardStats
import cz.cleansia.partner.domain.models.dashboard.EarningsSummary
import cz.cleansia.partner.domain.models.dashboard.UpcomingOrder
import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.domain.repositories.DashboardRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
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
    val userName: String = ""
)

enum class GreetingType {
    MORNING, AFTERNOON, EVENING
}

@HiltViewModel
class DashboardViewModel @Inject constructor(
    private val dashboardRepository: DashboardRepository,
    private val tokenManager: TokenManager
) : ViewModel() {

    private val _uiState = MutableStateFlow(DashboardUiState())
    val uiState: StateFlow<DashboardUiState> = _uiState.asStateFlow()

    init {
        loadUserName()
        updateGreeting()
        loadDashboardData()
    }

    private fun loadUserName() {
        val fullName = tokenManager.getUserFullName()
        // Use first name only for a friendlier greeting
        val firstName = fullName.split(" ").firstOrNull()?.takeIf { it.isNotBlank() } ?: ""
        _uiState.update { it.copy(userName = firstName) }
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
                is ApiResult.Error -> errors.add(statsResult.error.getUserMessage())
            }

            when (ordersResult) {
                is ApiResult.Success -> _uiState.update { it.copy(upcomingOrders = ordersResult.data) }
                is ApiResult.Error -> errors.add(ordersResult.error.getUserMessage())
            }

            when (earningsResult) {
                is ApiResult.Success -> _uiState.update { it.copy(earnings = earningsResult.data) }
                is ApiResult.Error -> errors.add(earningsResult.error.getUserMessage())
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
                is ApiResult.Error -> _uiState.update { it.copy(error = statsResult.error.getUserMessage()) }
            }

            when (ordersResult) {
                is ApiResult.Success -> _uiState.update { it.copy(upcomingOrders = ordersResult.data) }
                is ApiResult.Error -> { }
            }

            when (earningsResult) {
                is ApiResult.Success -> _uiState.update { it.copy(earnings = earningsResult.data) }
                is ApiResult.Error -> { }
            }

            _uiState.update { it.copy(isRefreshing = false) }
        }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null) }
    }
}
