package cz.cleansia.partner.features.payroll

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.payroll.PeriodPayRepository
import cz.cleansia.partner.data.payroll.PeriodPaySummary
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed interface PeriodPayUiState {
    data object Loading : PeriodPayUiState
    data object Error : PeriodPayUiState
    data class Loaded(val summary: PeriodPaySummary) : PeriodPayUiState
}

/**
 * Read-only "my period pay" — the per-order pay rollup for one pay period,
 * scoped to the signed-in cleaner (EmployeeId resolved from the local
 * profile, never from screen input; the backend re-checks against the
 * session anyway). No settlement actions exist on this surface by design.
 */
@HiltViewModel
class PeriodPayViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val periodPayRepository: PeriodPayRepository,
    private val userProfileStore: UserProfileStore,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val payPeriodId: String = savedStateHandle.get<String>("payPeriodId")
        ?: error("payPeriodId required for PeriodPay")

    /** Threaded through from the launching invoice — the summary DTO carries no currency. */
    val currencyCode: String? = savedStateHandle.get<String>("currencyCode")

    private val _state = MutableStateFlow<PeriodPayUiState>(PeriodPayUiState.Loading)
    val state: StateFlow<PeriodPayUiState> = _state.asStateFlow()

    init {
        load()
    }

    fun load() {
        viewModelScope.launch {
            _state.value = PeriodPayUiState.Loading
            val employeeId = userProfileStore.current()?.employeeId
            if (employeeId.isNullOrBlank()) {
                _state.value = PeriodPayUiState.Error
                return@launch
            }
            when (val result = periodPayRepository.getPeriodPays(employeeId, payPeriodId)) {
                is ApiResult.Success -> _state.value = PeriodPayUiState.Loaded(result.data)
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _state.value = PeriodPayUiState.Error
                }
            }
        }
    }
}
