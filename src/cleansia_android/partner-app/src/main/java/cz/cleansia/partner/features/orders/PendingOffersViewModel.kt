package cz.cleansia.partner.features.orders

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.CurrencyListItem
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.data.orders.PendingOffer
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed interface PendingOffersUiState {
    data object Loading : PendingOffersUiState
    data object Error : PendingOffersUiState
    data class Loaded(val offers: List<PendingOffer>) : PendingOffersUiState
}

enum class OfferAction { Confirm, Decline }

data class OfferAttempt(
    val orderId: String,
    val displayOrderNumber: String?,
    val action: OfferAction,
)

private enum class LoadPhase { Loading, Ready, Failed }

/**
 * Jobs a customer asked for this cleaner by name, held for them until a deadline the server owns.
 *
 * Confirming is [OrdersRepository.takeOrder] — the platform has no confirm command, and a second
 * acquisition path would either duplicate TakeOrder's one ordered gate or be weaker than it. The
 * take runs inside the contract sheet, on the swipe under the text; its verdict comes back through
 * [onWorkContractOutcome]. Because nothing gates the reservation on the weekly cap, that gate can
 * refuse a job the cleaner was told was theirs; the refusal is kept as [ActionState.Error] beside the
 * [attempt] it belongs to so the screen can say whose fault it is, rather than dropped on a snackbar
 * as a bare reason.
 */
@HiltViewModel
class PendingOffersViewModel @Inject constructor(
    private val ordersRepository: OrdersRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val phase = MutableStateFlow(LoadPhase.Loading)

    val uiState: StateFlow<PendingOffersUiState> =
        combine(ordersRepository.pendingOffers, phase) { offers, loadPhase ->
            when {
                offers.isNotEmpty() -> PendingOffersUiState.Loaded(offers)
                loadPhase == LoadPhase.Loading -> PendingOffersUiState.Loading
                loadPhase == LoadPhase.Failed -> PendingOffersUiState.Error
                else -> PendingOffersUiState.Loaded(offers)
            }
        }.stateIn(viewModelScope, SharingStarted.Eagerly, PendingOffersUiState.Loading)

    private val _actionState = MutableStateFlow<ActionState>(ActionState.Idle)
    val actionState: StateFlow<ActionState> = _actionState.asStateFlow()

    private val _attempt = MutableStateFlow<OfferAttempt?>(null)
    val attempt: StateFlow<OfferAttempt?> = _attempt.asStateFlow()

    private val _confirmed = MutableSharedFlow<String>(extraBufferCapacity = 1)
    val confirmed: SharedFlow<String> = _confirmed.asSharedFlow()

    private val _offerRefusal = MutableStateFlow<OfferRefusal?>(null)
    val offerRefusal: StateFlow<OfferRefusal?> = _offerRefusal.asStateFlow()

    private val _contractRequest = MutableStateFlow<WorkContractRequest.Take?>(null)
    val contractRequest: StateFlow<WorkContractRequest.Take?> = _contractRequest.asStateFlow()

    private var opened: PendingOffer? = null

    /**
     * The job the sheet's taken panel shows once the take succeeded: the offer's own facts at once, then the job as it
     * is read back as hers, which brings the door. Null while no take has succeeded or once the panel is closed.
     */
    private val _takenRow = MutableStateFlow<OrderListItem?>(null)
    val takenRow: StateFlow<OrderListItem?> = _takenRow.asStateFlow()

    init {
        if (ordersRepository.arePendingOffersStale()) {
            refresh()
        } else {
            phase.value = LoadPhase.Ready
        }
    }

    fun refresh() {
        viewModelScope.launch { fetch() }
    }

    fun onResume() {
        if (ordersRepository.arePendingOffersStale()) refresh()
    }

    fun confirm(offer: PendingOffer) {
        if (_actionState.value is ActionState.Submitting) return
        opened = offer
        _contractRequest.value = WorkContractRequest.Take(offer.id)
    }

    fun dismissContract() {
        _contractRequest.value = null
        _takenRow.value = null
    }

    /** Open job on the taken panel: the panel closes and the screen opens the job. */
    fun openTakenJob() {
        val id = _takenRow.value?.id ?: return
        dismissContract()
        _confirmed.tryEmit(id)
    }

    /**
     * The sheet's verdict on the offer it was opened for, framed as the confirm it stands for. A take keeps the sheet
     * up on its taken panel; a refusal closes it.
     */
    fun onWorkContractOutcome(outcome: WorkContractOutcome) {
        val orderId = (outcome.request as? WorkContractRequest.Take)?.orderId
        val offer = orderId?.let { id -> opened?.takeIf { it.id == id } ?: ordersRepository.pendingOffers.value.firstOrNull { it.id == id } }
        if (outcome is WorkContractOutcome.Taken && offer != null) _takenRow.value = offer.asTakenRow() else _contractRequest.value = null
        orderId ?: return
        runAction(orderId, offer?.displayOrderNumber, OfferAction.Confirm) { outcome.asResult() }
    }

    fun decline(offer: PendingOffer) =
        runAction(offer.id, offer.displayOrderNumber, OfferAction.Decline) { ordersRepository.declinePreferredOffer(it) }

    fun dismissRefusal() {
        _actionState.value = ActionState.Idle
        _attempt.value = null
        _offerRefusal.value = null
    }

    private suspend fun fetch() {
        when (ordersRepository.refreshPendingOffers()) {
            is ApiResult.Success -> phase.value = LoadPhase.Ready
            is ApiResult.Error -> phase.value = LoadPhase.Failed
        }
    }

    private suspend fun readBack(orderId: String) {
        val order = (ordersRepository.getById(orderId) as? ApiResult.Success)?.data ?: return
        if (_takenRow.value?.id == orderId) _takenRow.value = order.asBoardRow()
    }

    private fun runAction(
        orderId: String,
        displayOrderNumber: String?,
        action: OfferAction,
        block: suspend (String) -> ApiResult<Unit>,
    ) {
        if (_actionState.value is ActionState.Submitting) return
        _attempt.value = OfferAttempt(orderId, displayOrderNumber, action)
        _actionState.value = ActionState.Submitting
        _offerRefusal.value = null

        viewModelScope.launch {
            when (val result = block(orderId)) {
                is ApiResult.Success -> {
                    _actionState.value = ActionState.Idle
                    _attempt.value = null
                    when {
                        action == OfferAction.Decline -> snackbar.showSuccessKey(R.string.offer_declined_toast)
                        _takenRow.value?.id == orderId -> readBack(orderId)
                        else -> _confirmed.emit(orderId)
                    }
                }
                is ApiResult.Error -> {
                    // Both refusals are framed by the screen, in their own words: a snackbar carrying
                    // the same bare reason would land on top of the sentence that explains it.
                    val reason = errorTranslator.translate(result.error)
                    _actionState.value = ActionState.Error(reason)
                    _attempt.value = null
                    _offerRefusal.value = OfferRefusal(action, displayOrderNumber, reason)
                }
            }
            // The server decides whether the offer survived either outcome — a confirm the cap refused
            // leaves the reservation live, a taken seat removes it.
            fetch()
        }
    }
}

/** The offer as the taken panel's row: its area, day, window and reward, and no door until the job is read back. */
internal fun PendingOffer.asTakenRow(): OrderListItem = OrderListItem(
    id = id,
    displayOrderNumber = displayOrderNumber,
    customerAddressApproximate = customerAddressApproximate,
    rooms = rooms,
    bathrooms = bathrooms,
    cleaningDateTime = cleaningDateTime,
    estimatedTime = estimatedTime,
    currency = currencyCode?.let { CurrencyListItem(code = it) },
    estimatedCleanerPay = estimatedCleanerPay,
)
