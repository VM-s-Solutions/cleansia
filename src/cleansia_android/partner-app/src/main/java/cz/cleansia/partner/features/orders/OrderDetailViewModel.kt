package cz.cleansia.partner.features.orders

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.PendingOfferItem
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.orders.OrdersRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

/** Per-action discriminator so individual buttons can show their own spinners. */
enum class OrderAction { Take, Start, NotifyOnTheWay, MarkCashCollected, Complete, DeclineOffer }

sealed interface OrderDetailUiState {
    data object Loading : OrderDetailUiState
    data object Error : OrderDetailUiState
    data class Loaded(val order: OrderItem) : OrderDetailUiState
}

@HiltViewModel
class OrderDetailViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val ordersRepository: OrdersRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val orderId: String = savedStateHandle.get<String>("orderId")
        ?: error("orderId required for OrderDetail route")

    private val _uiState = MutableStateFlow<OrderDetailUiState>(OrderDetailUiState.Loading)
    val uiState: StateFlow<OrderDetailUiState> = _uiState.asStateFlow()

    private val _actionState = MutableStateFlow<ActionState>(ActionState.Idle)
    val actionState: StateFlow<ActionState> = _actionState.asStateFlow()

    private val _inFlightAction = MutableStateFlow<OrderAction?>(null)
    val inFlightAction: StateFlow<OrderAction?> = _inFlightAction.asStateFlow()

    /**
     * The reservation held for this cleaner on this order, if there is one. The partner order DTO
     * carries no reservation block — that field is customer-only, so no cleaner ever learns an order
     * was reserved for someone else — so the disclosure is composed from the cleaner's own offers.
     * Absent means an ordinary job, which is exactly right for the short-lead band, where the push
     * fires but nothing is withheld.
     */
    val preferredOffer: StateFlow<PendingOfferItem?> = ordersRepository.pendingOffers
        .map { offers -> offers.firstOrNull { it.id == orderId } }
        .stateIn(viewModelScope, SharingStarted.Eagerly, null)

    init {
        ensureFreshOrCachedAsync()
        ensureOffersFresh()
    }

    /**
     * Background-freshness gate used by init, ON_RESUME, and post-mutation
     * callbacks. Skips the network entirely when the per-order cache is still
     * warm — keeps the sheet stable and avoids a needless round-trip every
     * time the cleaner pops back from a sub-screen. A loaded order stays
     * mounted through a background re-fetch (no spinner flash).
     */
    fun ensureFreshOrCachedAsync() {
        if (!ordersRepository.isOrderStale(orderId)) return
        viewModelScope.launch { fetch() }
    }

    fun refresh() {
        viewModelScope.launch { fetch() }
    }

    fun onResume() {
        ensureFreshOrCachedAsync()
        ensureOffersFresh()
    }

    private fun ensureOffersFresh() {
        if (!ordersRepository.arePendingOffersStale()) return
        viewModelScope.launch { ordersRepository.refreshPendingOffers() }
    }

    /** Refusing the reservation from the job it belongs to; the same one write the offers list makes. */
    fun declinePreferredOffer() = runAction(OrderAction.DeclineOffer) {
        ordersRepository.declinePreferredOffer(orderId)
    }

    fun dismissActionError() {
        if (_actionState.value is ActionState.Error) _actionState.value = ActionState.Idle
    }

    /**
     * @param notifyOnError raise the translated failure on the snackbar. False
     * only for the reconciling fetch that follows a *rejected* action: the
     * cleaner has already been told why the action failed, and the partner app
     * has no `NetworkErrorInterceptor` (only the customer app wires one) to
     * collapse the duplicate for us. A silent failure is fine there because the
     * fetch is a best-effort reconciliation, not something the user asked for.
     */
    private suspend fun fetch(notifyOnError: Boolean = true) {
        when (val result = ordersRepository.getById(orderId)) {
            is ApiResult.Success -> _uiState.value = OrderDetailUiState.Loaded(result.data)
            is ApiResult.Error -> {
                if (notifyOnError) snackbar.showError(errorTranslator.translate(result.error))
                if (_uiState.value !is OrderDetailUiState.Loaded) {
                    _uiState.value = OrderDetailUiState.Error
                }
            }
        }
    }

    fun take() = runAction(OrderAction.Take) { ordersRepository.takeOrder(orderId) }
    fun start() = runAction(OrderAction.Start) { ordersRepository.startOrder(orderId) }
    fun notifyOnTheWay() = runAction(OrderAction.NotifyOnTheWay) { ordersRepository.notifyOnTheWay(orderId) }

    fun markCashCollected() = runAction(OrderAction.MarkCashCollected) {
        ordersRepository.markCashCollected(orderId)
    }

    fun complete(actualMinutes: Int?, notes: String?) = runAction(OrderAction.Complete) {
        ordersRepository.completeOrder(orderId, actualMinutes, notes)
    }

    fun onContentMutated() = ensureFreshOrCachedAsync()

    private fun runAction(action: OrderAction, block: suspend () -> ApiResult<Unit>) {
        if (_actionState.value is ActionState.Submitting) return
        _actionState.value = ActionState.Submitting
        _inFlightAction.value = action
        viewModelScope.launch {
            when (val result = block()) {
                is ApiResult.Success -> {
                    if (action == OrderAction.Complete) {
                        snackbar.showSuccessKey(R.string.order_completed_toast)
                    }
                    if (action == OrderAction.DeclineOffer) {
                        snackbar.showSuccessKey(R.string.offer_declined_toast)
                    }
                    _actionState.value = ActionState.Idle
                    _inFlightAction.value = null
                    fetch()
                }
                is ApiResult.Error -> {
                    // A confirm the take gate refuses on a job the cleaner was TOLD was theirs is
                    // framed by the screen as ours rather than theirs; a snackbar carrying the bare
                    // reason would land on top of that sentence.
                    val disclosedOffer = action == OrderAction.Take && preferredOffer.value != null
                    if (!disclosedOffer) {
                        snackbar.showError(errorTranslator.translate(result.error))
                    }
                    _actionState.value = ActionState.Error(errorTranslator.translate(result.error))
                    _inFlightAction.value = null
                    // A clean reject almost always means the order moved on
                    // without us — another cleaner took it, or the status
                    // advanced from a different device. Without this the footer
                    // keeps offering the exact action the server just refused,
                    // so the cleaner taps it again and again. Straight to
                    // fetch(), not ensureFreshOrCachedAsync(): the cached copy
                    // is known to disagree with the server, which is precisely
                    // when a warm cache must not win. That also makes
                    // invalidateOrder() redundant here.
                    fetch(notifyOnError = false)
                }
            }
        }
    }
}
