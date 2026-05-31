package cz.cleansia.partner.features.orders.viewmodels

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.R
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.data.orders.OrdersRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

/** Per-action progress states so individual buttons can show their own spinners. */
enum class OrderAction { Take, Start, NotifyOnTheWay, Complete }

/**
 * Three-way split of the legacy `isLoading` flag so the screen can tell the
 * difference between the first-ever paint (full-page spinner OK), a
 * user-initiated pull (chunky PTR indicator OK), and a silent background
 * refresh after a mutation or ON_RESUME (no visible spinner — content
 * updates in place once the network responds).
 *
 * OrderDetailsScreen has no PullToRefreshBox today, so `isUserRefreshing`
 * is currently unused by the UI — but it's kept in the contract so a
 * future pull-to-refresh wrap can drive PTR without further VM changes,
 * matching the convention used by other Cleansia detail/list screens.
 */
data class OrderDetailsUiState(
    val isInitialLoad: Boolean = true,
    val isUserRefreshing: Boolean = false,
    val isBackgroundRefreshing: Boolean = false,
    val order: OrderItem? = null,
    val inFlight: OrderAction? = null,
    val error: String? = null,
    val isCompletedJustNow: Boolean = false,
)

@HiltViewModel
class OrderDetailsViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val ordersRepository: OrdersRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    // Compose Nav 2.8 typed routes expose data-class property names as
    // SavedStateHandle keys, so this keeps working without a toRoute<>() call.
    private val orderId: String = savedStateHandle.get<String>("orderId")
        ?: error("orderId required for OrderDetails route")

    private val _uiState = MutableStateFlow(OrderDetailsUiState())
    val uiState: StateFlow<OrderDetailsUiState> = _uiState.asStateFlow()

    init {
        // First paint uses the stale-checked path. The cache is empty on
        // a cold ViewModel so isStale() returns true and a fetch fires
        // anyway, but isInitialLoad stays true through that first fetch
        // so the full-page spinner shows. Warm caches (process kept
        // alive, repository singleton) will short-circuit to in-place
        // render instead of flashing the spinner — matches what the
        // user already saw.
        ensureFreshOrCachedAsync()
    }

    /**
     * Background-freshness gate used by init, ON_RESUME, and post-mutation
     * callbacks (photo upload, note add, etc.). Skips the network entirely
     * when the per-order cache is still warm (<30s by default) — keeps
     * sheet state stable and avoids a needless round-trip every time the
     * cleaner pops back from a sub-screen.
     *
     * Loading flag policy:
     *  - If we've never rendered the order (initial paint), keep
     *    `isInitialLoad = true` until the fetch returns so the full-page
     *    spinner stays visible.
     *  - If we already have an order shown, flip only
     *    `isBackgroundRefreshing = true` — no PTR indicator, no full-page
     *    spinner. The detail content stays mounted and the new payload
     *    swaps in once it arrives.
     */
    fun ensureFreshOrCachedAsync() {
        if (!ordersRepository.isOrderStale(orderId)) return
        viewModelScope.launch { fetch(isUser = false) }
    }

    /**
     * User-initiated refresh. Always fetches regardless of cache age and
     * drives `isUserRefreshing` so a hypothetical PullToRefreshBox would
     * show its indicator. The detail screen doesn't currently expose a
     * pull gesture (BottomSheetScaffold), but keeping the public refresh()
     * symmetrical with list screens means future UI work doesn't have to
     * touch the VM.
     */
    fun refresh() {
        viewModelScope.launch { fetch(isUser = true) }
    }

    /** Lifecycle hook from the screen's ON_RESUME effect. */
    fun onResume() = ensureFreshOrCachedAsync()

    private suspend fun fetch(isUser: Boolean) {
        _uiState.update {
            if (it.order == null) {
                // No data on screen yet — keep the full-page spinner up.
                // isUserRefreshing stays false so we don't double-render
                // PTR over the spinner on a hypothetical pull during cold
                // load.
                it.copy(isInitialLoad = true, error = null)
            } else {
                it.copy(
                    isUserRefreshing = isUser,
                    isBackgroundRefreshing = !isUser,
                    error = null,
                )
            }
        }
        when (val result = ordersRepository.getById(orderId)) {
            is ApiResult.Success -> _uiState.update {
                it.copy(
                    isInitialLoad = false,
                    isUserRefreshing = false,
                    isBackgroundRefreshing = false,
                    order = result.data,
                )
            }
            is ApiResult.Error -> {
                snackbar.showError(errorTranslator.translate(result.error))
                _uiState.update {
                    it.copy(
                        isInitialLoad = false,
                        isUserRefreshing = false,
                        isBackgroundRefreshing = false,
                    )
                }
            }
        }
    }

    fun take() = runAction(OrderAction.Take) { ordersRepository.takeOrder(orderId) }
    fun start() = runAction(OrderAction.Start) { ordersRepository.startOrder(orderId) }
    fun notifyOnTheWay() = runAction(OrderAction.NotifyOnTheWay) { ordersRepository.notifyOnTheWay(orderId) }

    fun complete(actualMinutes: Int?, notes: String?) = runAction(OrderAction.Complete) {
        ordersRepository.completeOrder(orderId, actualMinutes, notes)
    }

    /**
     * Called by PhotosSection / NotesAndIssuesSection after a successful
     * mutation. The repository has already invalidated the per-order
     * staleness watermark for us (on the mutation's success path), so
     * `ensureFreshOrCachedAsync()` falls through to a silent background
     * fetch that updates the sheet content without flashing a spinner —
     * the cleaner just sees the new photo / note appear.
     */
    fun onContentMutated() = ensureFreshOrCachedAsync()

    private fun runAction(action: OrderAction, block: suspend () -> ApiResult<Unit>) {
        viewModelScope.launch {
            _uiState.update { it.copy(inFlight = action, error = null) }
            when (val result = block()) {
                is ApiResult.Success -> {
                    // Push the success toast straight onto the global
                    // snackbar bus — the screen no longer threads
                    // SnackbarHostState through composables.
                    if (action == OrderAction.Complete) {
                        snackbar.showSuccessKey(R.string.order_completed_toast)
                    }
                    _uiState.update {
                        it.copy(
                            inFlight = null,
                            isCompletedJustNow = action == OrderAction.Complete,
                        )
                    }
                    // Silent refetch: the repository already invalidated
                    // its staleness watermark on the mutation success
                    // path, so this falls through to a background fetch
                    // that swaps the new server state in without flashing
                    // the full-page spinner. The button's own per-action
                    // inFlight spinner gave the user feedback already.
                    fetch(isUser = false)
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(inFlight = null) }
                }
            }
        }
    }

    fun clearError() = _uiState.update { it.copy(error = null) }
    fun clearCompletedJustNow() = _uiState.update { it.copy(isCompletedJustNow = false) }
}

