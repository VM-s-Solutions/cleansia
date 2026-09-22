package cz.cleansia.customer.features.orders

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.orders.WorkContractDto
import cz.cleansia.customer.core.settings.AppSettingsRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface WorkContractUiState {
    data object Loading : WorkContractUiState
    data object Error : WorkContractUiState
    data class Loaded(val contract: WorkContractDto) : WorkContractUiState
}

/**
 * The contract for work a cleaner accepted for one seat of the customer's order, as the server
 * rendered it: the accepted document's text in the customer's UI language, the job facts frozen at
 * the acceptance, and the acceptance itself. Keyed on the acceptance, so a dropped cleaner's contract
 * stays readable; a retry re-asks in the language current at that moment.
 *
 * A refused read (another customer's acceptance, a stale id) is the error state with nothing of the
 * order on it; the repository's message rides the snackbar, as the order detail's own load does.
 */
@HiltViewModel
class WorkContractViewModel @Inject constructor(
    private val orderRepository: OrderRepository,
    private val appSettingsRepository: AppSettingsRepository,
    private val snackbar: SnackbarController,
    savedStateHandle: SavedStateHandle,
) : ViewModel() {

    private val acceptanceId: String? = savedStateHandle.get<String>("acceptanceId")

    private val _state = MutableStateFlow<WorkContractUiState>(WorkContractUiState.Loading)
    val state: StateFlow<WorkContractUiState> = _state.asStateFlow()

    init {
        refresh()
    }

    fun refresh() {
        val id = acceptanceId
        if (id.isNullOrBlank()) {
            _state.value = WorkContractUiState.Error
            return
        }
        _state.value = WorkContractUiState.Loading
        viewModelScope.launch {
            val language = appSettingsRepository.emailLanguageTag()
            val contract = orderRepository.getWorkContract(id, language)
                .onError { error -> if (error !is ApiError.Network) snackbar.showError(error) }
                .getOrNull()
            _state.value = if (contract != null) WorkContractUiState.Loaded(contract) else WorkContractUiState.Error
        }
    }
}
