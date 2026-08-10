package cz.cleansia.partner.features.orders

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.PendingOfferItem
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.orders.OrdersRepository
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
    data class Loaded(val offers: List<PendingOfferItem>) : PendingOffersUiState
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
 * acquisition path would either duplicate TakeOrder's one ordered gate or be weaker than it. Because
 * nothing gates the reservation on the weekly cap, that gate can refuse a job the cleaner was told was
 * theirs; the refusal is kept as [ActionState.Error] beside the [attempt] it belongs to so the screen
 * can say whose fault it is, rather than dropped on a snackbar as a bare reason.
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

    fun confirm(offer: PendingOfferItem) =
        runAction(offer, OfferAction.Confirm) { ordersRepository.takeOrder(it) }

    fun decline(offer: PendingOfferItem) =
        runAction(offer, OfferAction.Decline) { ordersRepository.declinePreferredOffer(it) }

    fun dismissRefusal() {
        _actionState.value = ActionState.Idle
        _attempt.value = null
    }

    private suspend fun fetch() {
        when (ordersRepository.refreshPendingOffers()) {
            is ApiResult.Success -> phase.value = LoadPhase.Ready
            is ApiResult.Error -> phase.value = LoadPhase.Failed
        }
    }

    private fun runAction(
        offer: PendingOfferItem,
        action: OfferAction,
        block: suspend (String) -> ApiResult<Unit>,
    ) {
        val orderId = offer.id ?: return
        if (_actionState.value is ActionState.Submitting) return
        _attempt.value = OfferAttempt(orderId, offer.displayOrderNumber, action)
        _actionState.value = ActionState.Submitting

        viewModelScope.launch {
            when (val result = block(orderId)) {
                is ApiResult.Success -> {
                    _actionState.value = ActionState.Idle
                    _attempt.value = null
                    if (action == OfferAction.Decline) {
                        snackbar.showSuccessKey(R.string.offer_declined_toast)
                    } else {
                        _confirmed.emit(orderId)
                    }
                }
                is ApiResult.Error -> {
                    val reason = errorTranslator.translate(result.error)
                    when (action) {
                        // The screen frames this one; a snackbar carrying the same bare reason would
                        // land on top of the sentence that says the platform, not the cleaner, is at
                        // fault.
                        OfferAction.Confirm -> _actionState.value = ActionState.Error(reason)
                        OfferAction.Decline -> {
                            _actionState.value = ActionState.Idle
                            _attempt.value = null
                            snackbar.showError(reason)
                        }
                    }
                }
            }
            // The server decides whether the offer survived either outcome — a confirm the cap refused
            // leaves the reservation live, a taken seat removes it.
            fetch()
        }
    }
}
