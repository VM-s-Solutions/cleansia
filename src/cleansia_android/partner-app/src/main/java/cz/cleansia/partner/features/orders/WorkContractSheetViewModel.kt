package cz.cleansia.partner.features.orders

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.data.orders.WorkContract
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

/** What the sheet is opened for. The take and the standalone acceptance swipe; a read only reads. */
sealed interface WorkContractRequest {
    data class Take(val orderId: String) : WorkContractRequest
    data class Accept(val orderId: String) : WorkContractRequest
    data class Read(val acceptanceId: String) : WorkContractRequest
}

sealed interface WorkContractUiState {
    data object Loading : WorkContractUiState
    data object Error : WorkContractUiState

    /** The order has no contract text in force, so it cannot be taken now; nothing to accept. */
    data object Unavailable : WorkContractUiState
    data class Loaded(val contract: WorkContract) : WorkContractUiState
}

enum class WorkContractNotice { TextUpdated }

/**
 * How the sheet ended for its host. The host — the board, the detail or the offers list — reacts to
 * the take exactly as it did when the take was its own one tap: a success refreshes its panes, a
 * refusal is framed and reconciled in its own words. The two contract keys never leave the sheet.
 */
sealed interface WorkContractOutcome {
    val request: WorkContractRequest

    data class Taken(override val request: WorkContractRequest.Take) : WorkContractOutcome
    data class Accepted(override val request: WorkContractRequest.Accept) : WorkContractOutcome
    data class Refused(override val request: WorkContractRequest, val error: ApiError) : WorkContractOutcome

    fun asResult(): ApiResult<Unit> = when (this) {
        is Taken, is Accepted -> ApiResult.Success(Unit)
        is Refused -> ApiResult.Error(error)
    }
}

/**
 * The contract sheet: loads the preview (or the accepted contract), and on the swipe echoes the
 * previewed text row to the take or the standalone acceptance.
 *
 * `contract.text_mismatch` is the one refusal handled here: the echoed text is not this order's, so
 * the preview is re-run, the gesture reset and the cleaner told to read again. `legal.document_not_found`
 * can only come from the preview and means the job cannot be taken now. Every other refusal is the
 * host's to frame.
 */
@HiltViewModel
class WorkContractSheetViewModel @Inject constructor(
    private val ordersRepository: OrdersRepository,
    private val appSettingsRepository: AppSettingsRepository,
) : ViewModel() {

    private var request: WorkContractRequest? = null

    private val _uiState = MutableStateFlow<WorkContractUiState>(WorkContractUiState.Loading)
    val uiState: StateFlow<WorkContractUiState> = _uiState.asStateFlow()

    private val _actionState = MutableStateFlow<ActionState>(ActionState.Idle)
    val actionState: StateFlow<ActionState> = _actionState.asStateFlow()

    private val _notice = MutableStateFlow<WorkContractNotice?>(null)
    val notice: StateFlow<WorkContractNotice?> = _notice.asStateFlow()

    private val _outcome = MutableSharedFlow<WorkContractOutcome>(extraBufferCapacity = 1)
    val outcome: SharedFlow<WorkContractOutcome> = _outcome.asSharedFlow()

    /** Every open loads afresh: the preview is the server's word at that moment, never a cached one. */
    fun open(request: WorkContractRequest) {
        this.request = request
        _notice.value = null
        _actionState.value = ActionState.Idle
        load()
    }

    fun retry() = load()

    private fun load() {
        val request = request ?: return
        _uiState.value = WorkContractUiState.Loading
        viewModelScope.launch {
            val language = appSettingsRepository.emailLanguageTag()
            val result = when (request) {
                is WorkContractRequest.Take -> ordersRepository.getWorkContractPreview(request.orderId, language)
                is WorkContractRequest.Accept -> ordersRepository.getWorkContractPreview(request.orderId, language)
                is WorkContractRequest.Read -> ordersRepository.getWorkContract(request.acceptanceId, language)
            }
            _uiState.value = when (result) {
                is ApiResult.Success -> WorkContractUiState.Loaded(result.data)
                is ApiResult.Error ->
                    if (result.error.hasKey(DOCUMENT_NOT_FOUND)) WorkContractUiState.Unavailable
                    else WorkContractUiState.Error
            }
        }
    }

    /** The swipe. Echoes the text row on screen; a read has nothing to swipe. */
    fun accept() {
        val request = request ?: return
        val contract = (_uiState.value as? WorkContractUiState.Loaded)?.contract ?: return
        if (_actionState.value is ActionState.Submitting) return
        val textId = contract.legalDocumentTextId
        val submit: suspend () -> WorkContractOutcome = when (request) {
            is WorkContractRequest.Take -> {
                { ordersRepository.takeOrder(request.orderId, textId).toOutcome(request) { WorkContractOutcome.Taken(request) } }
            }
            is WorkContractRequest.Accept -> {
                { ordersRepository.acceptWorkContract(request.orderId, textId).toOutcome(request) { WorkContractOutcome.Accepted(request) } }
            }
            is WorkContractRequest.Read -> return
        }
        _actionState.value = ActionState.Submitting
        viewModelScope.launch {
            val outcome = submit()
            if (outcome is WorkContractOutcome.Refused && outcome.error.hasKey(TEXT_MISMATCH)) {
                _notice.value = WorkContractNotice.TextUpdated
                _actionState.value = ActionState.Idle
                load()
                return@launch
            }
            _actionState.value = ActionState.Idle
            _outcome.emit(outcome)
        }
    }

    private inline fun ApiResult<Unit>.toOutcome(
        request: WorkContractRequest,
        success: () -> WorkContractOutcome,
    ): WorkContractOutcome = when (this) {
        is ApiResult.Success -> success()
        is ApiResult.Error -> WorkContractOutcome.Refused(request, error)
    }

    private companion object {
        const val TEXT_MISMATCH = "contract.text_mismatch"
        const val DOCUMENT_NOT_FOUND = "legal.document_not_found"
    }
}

internal fun ApiError.hasKey(key: String): Boolean {
    val badRequest = this as? ApiError.BadRequest ?: return false
    return badRequest.errorKey == key || badRequest.validationErrors?.values?.any { key in it } == true
}
