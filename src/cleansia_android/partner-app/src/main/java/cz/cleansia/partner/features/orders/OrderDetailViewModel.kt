package cz.cleansia.partner.features.orders

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.orders.OrdersRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

/** Per-action discriminator so individual buttons can show their own spinners. */
enum class OrderAction { Take, Start, NotifyOnTheWay, MarkCashCollected, Complete }

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

    init {
        ensureFreshOrCachedAsync()
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

    fun onResume() = ensureFreshOrCachedAsync()

    private suspend fun fetch() {
        when (val result = ordersRepository.getById(orderId)) {
            is ApiResult.Success -> _uiState.value = OrderDetailUiState.Loaded(result.data)
            is ApiResult.Error -> {
                snackbar.showError(errorTranslator.translate(result.error))
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
                    _actionState.value = ActionState.Idle
                    _inFlightAction.value = null
                    fetch()
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _actionState.value = ActionState.Error(errorTranslator.translate(result.error))
                    _inFlightAction.value = null
                }
            }
        }
    }
}
