package cz.cleansia.partner.features.earnings.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.DashboardStatsDto
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.data.dashboard.DashboardRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class EarningsSummaryUiState(
    val isLoading: Boolean = false,
    val stats: DashboardStatsDto? = null,
)

/**
 * Thin VM for the Pay & Earnings summary screen. Re-uses
 * [DashboardRepository.getStats] — same data the dashboard hero
 * cards already render, just on its own dedicated surface so the
 * earnings card can drill into something meaningful before the
 * cleaner has any invoices generated yet.
 */
@HiltViewModel
class EarningsSummaryViewModel @Inject constructor(
    private val dashboardRepository: DashboardRepository,
    private val snackbar: SnackbarController,
    private val errorTranslator: ApiErrorTranslator,
) : ViewModel() {

    private val _uiState = MutableStateFlow(EarningsSummaryUiState())
    val uiState: StateFlow<EarningsSummaryUiState> = _uiState.asStateFlow()

    init {
        refresh()
    }

    fun refresh() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true) }
            when (val result = dashboardRepository.getStats(employeeId = null)) {
                is ApiResult.Success -> _uiState.update {
                    it.copy(isLoading = false, stats = result.data)
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(isLoading = false) }
                }
            }
        }
    }
}
