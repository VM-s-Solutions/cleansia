package cz.cleansia.partner.features.earnings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.dashboard.DashboardRepository
import cz.cleansia.partner.data.dashboard.DashboardStats
import cz.cleansia.partner.data.payroll.CashHeld
import cz.cleansia.partner.data.payroll.PeriodPayRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.async
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed interface EarningsSummaryUiState {
    data object Loading : EarningsSummaryUiState
    data object Error : EarningsSummaryUiState
    data class Loaded(val stats: DashboardStats) : EarningsSummaryUiState
}

/**
 * Thin VM for the Pay & Earnings summary screen. Re-uses
 * [DashboardRepository.getStats] — same data the dashboard hero
 * cards already render, just on its own dedicated surface so the
 * earnings card can drill into something meaningful before the
 * cleaner has any invoices generated yet. Beside it, the company cash
 * the cleaner holds.
 */
@HiltViewModel
class EarningsSummaryViewModel @Inject constructor(
    private val dashboardRepository: DashboardRepository,
    private val periodPayRepository: PeriodPayRepository,
    private val snackbar: SnackbarController,
    private val errorTranslator: ApiErrorTranslator,
) : ViewModel() {

    private val _uiState = MutableStateFlow<EarningsSummaryUiState>(EarningsSummaryUiState.Loading)
    val uiState: StateFlow<EarningsSummaryUiState> = _uiState.asStateFlow()

    private val _cashHeld = MutableStateFlow<List<CashHeld>>(emptyList())
    val cashHeld: StateFlow<List<CashHeld>> = _cashHeld.asStateFlow()

    init {
        refresh()
    }

    fun refresh() {
        viewModelScope.launch {
            if (_uiState.value !is EarningsSummaryUiState.Loaded) {
                _uiState.value = EarningsSummaryUiState.Loading
            }
            val cash = async { periodPayRepository.getCashHeld() }
            val statsLoaded = when (val result = dashboardRepository.getStats(employeeId = null)) {
                is ApiResult.Success -> {
                    _uiState.value = EarningsSummaryUiState.Loaded(result.data)
                    true
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    if (_uiState.value !is EarningsSummaryUiState.Loaded) {
                        _uiState.value = EarningsSummaryUiState.Error
                    }
                    false
                }
            }
            when (val result = cash.await()) {
                is ApiResult.Success -> _cashHeld.value = result.data
                is ApiResult.Error -> if (statsLoaded) snackbar.showError(errorTranslator.translate(result.error))
            }
        }
    }
}
