package cz.cleansia.partner.features.orders

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.core.auth.EmployeeIdResolver
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.data.orders.PendingOffer
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

/** Per-action discriminator so individual buttons can show their own spinners. */
enum class OrderAction { Take, AcceptContract, Start, NotifyOnTheWay, MarkCashCollected, Complete, DeclineOffer }

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
    private val employeeIdResolver: EmployeeIdResolver,
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
    val preferredOffer: StateFlow<PendingOffer?> = ordersRepository.pendingOffers
        .map { offers -> offers.firstOrNull { it.id == orderId } }
        .stateIn(viewModelScope, SharingStarted.Eagerly, null)

    private val _offerRefusal = MutableStateFlow<OfferRefusal?>(null)
    val offerRefusal: StateFlow<OfferRefusal?> = _offerRefusal.asStateFlow()

    /** The contract sheet the screen is showing, if any: a take, a standalone acceptance or a read. */
    private val _contractRequest = MutableStateFlow<WorkContractRequest?>(null)
    val contractRequest: StateFlow<WorkContractRequest?> = _contractRequest.asStateFlow()

    private val myEmployeeId = MutableStateFlow<String?>(null)

    /**
     * The caller's own acceptance, paired through their crew entry. Resolved here rather than in the
     * screen because the pairing needs the signed-in employee id, which only the resolver can supply.
     */
    val contractStanding: StateFlow<WorkContractStanding> =
        combine(_uiState, myEmployeeId) { state, employeeId ->
            (state as? OrderDetailUiState.Loaded)?.order?.workContractStanding(employeeId)
                ?: WorkContractStanding.None
        }.stateIn(viewModelScope, SharingStarted.Eagerly, WorkContractStanding.None)

    init {
        ensureFreshOrCachedAsync()
        ensureOffersFresh()
        viewModelScope.launch { myEmployeeId.value = employeeIdResolver.resolve() }
    }

    /**
     * Background-freshness gate used by init, ON_RESUME, and post-mutation
     * callbacks. Skips the network entirely when the per-order cache is still
     * warm — keeps the sheet stable and avoids a needless round-trip every
     * time the cleaner pops back from a sub-screen. A loaded order stays
     * mounted through a background re-fetch (no spinner flash).
     */
    fun ensureFreshOrCachedAsync() {
        // The repository stores a per-order WATERMARK, not the order: `fetch()` is the only thing that
        // puts one into [uiState], which starts at Loading and does not survive this view model. So a
        // warm watermark may only suppress the fetch once the sheet is already showing the order —
        // otherwise opening a job inside the freshness window left it spinning forever.
        if (uiState.value is OrderDetailUiState.Loaded && !ordersRepository.isOrderStale(orderId)) return
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

    fun dismissOfferRefusal() {
        _offerRefusal.value = null
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

    /** Taking is accepting the contract: the take happens inside the sheet, on the swipe. */
    fun take() {
        _contractRequest.value = WorkContractRequest.Take(orderId)
    }

    fun openContract(request: WorkContractRequest) {
        _contractRequest.value = request
    }

    fun dismissContract() {
        _contractRequest.value = null
    }

    /**
     * The sheet's verdict, handled exactly as the one-tap take used to be: a success refreshes the
     * order, a refusal is framed (on a disclosed offer) or snackbarred and reconciled.
     */
    fun onWorkContractOutcome(outcome: WorkContractOutcome) {
        _contractRequest.value = null
        val action = if (outcome.request is WorkContractRequest.Accept) OrderAction.AcceptContract else OrderAction.Take
        runAction(action) { outcome.asResult() }
    }

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
                    _offerRefusal.value = null
                    fetch()
                }
                is ApiResult.Error -> {
                    // A start or a completion on a seat with no acceptance is not an error to read but
                    // a contract to accept: the same sheet opens, in accept mode, and the gesture
                    // springs back to be retried once the row exists.
                    if ((action == OrderAction.Start || action == OrderAction.Complete) &&
                        result.error.hasKey(ACCEPTANCE_REQUIRED)
                    ) {
                        _actionState.value = ActionState.Idle
                        _inFlightAction.value = null
                        _contractRequest.value = WorkContractRequest.Accept(orderId)
                        return@launch
                    }
                    // A refusal ON A DISCLOSED OFFER is framed by the screen in its own words — the
                    // handover we could not make, or the release that changed nothing — so the bare
                    // reason must not also arrive as a snackbar on top of it. Everything else on this
                    // screen still snackbars exactly as before.
                    val reason = errorTranslator.translate(result.error)
                    val refusedOfferAction = preferredOffer.value?.let {
                        when (action) {
                            OrderAction.Take -> OfferAction.Confirm
                            OrderAction.DeclineOffer -> OfferAction.Decline
                            else -> null
                        }
                    }
                    if (refusedOfferAction == null) {
                        snackbar.showError(reason)
                    } else {
                        _offerRefusal.value = OfferRefusal(
                            refusedOfferAction,
                            preferredOffer.value?.displayOrderNumber,
                            reason,
                        )
                    }
                    _actionState.value = ActionState.Error(reason)
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

    private companion object {
        const val ACCEPTANCE_REQUIRED = "contract.acceptance_required"
    }
}
