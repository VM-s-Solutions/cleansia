package cz.cleansia.customer.features.booking

import android.util.Log
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.core.orders.OrderApi
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * UI state for [BookingSuccessScreen]'s enrichment block.
 *
 * The screen always renders its celebratory header + CTAs; this state only
 * drives the optional enrichment (confirmation code, arrival window, address,
 * total). On [Error], the screen silently drops the block rather than
 * surfacing a "we couldn't load your order" message — the user just booked,
 * the nav args already carry the confirmation code, and the OrdersTab will
 * show the full record on the next visit.
 */
sealed interface BookingSuccessUiState {
    data object Loading : BookingSuccessUiState
    data class Loaded(val order: OrderDetailDto) : BookingSuccessUiState
    data object Error : BookingSuccessUiState
}

/**
 * Fetches the newly-created order's detail once for the success screen so
 * the user sees their arrival window, address and total without having to
 * bounce to the Orders tab.
 *
 * Bypasses [OrderRepository.getById] on purpose: that helper surfaces a
 * snackbar on failure (by design for the Orders-tab flow), which would
 * muddy the post-payment celebration. Here we hit [OrderApi] directly and
 * fall back to a silent [BookingSuccessUiState.Error] — the screen degrades
 * gracefully.
 *
 * Side effect: pokes [OrderRepository.refresh] so when the user navigates
 * to the Orders tab afterwards their new order is already in the list.
 * This is deliberately fire-and-forget; the tab also re-fetches when the
 * repo is empty, so a failed refresh here is harmless.
 */
@HiltViewModel
class BookingSuccessViewModel @Inject constructor(
    private val orderApi: OrderApi,
    private val orderRepository: OrderRepository,
    savedStateHandle: SavedStateHandle,
) : ViewModel() {

    private val orderId: String? = savedStateHandle.get<String>("orderId")

    private val _state = MutableStateFlow<BookingSuccessUiState>(BookingSuccessUiState.Loading)
    val state: StateFlow<BookingSuccessUiState> = _state.asStateFlow()

    init {
        // Pre-warm the Orders list so the new order is visible the moment the
        // user lands on the Orders tab. Failure is silent — the tab refreshes
        // itself when it's empty.
        viewModelScope.launch {
            orderRepository.refresh()
        }
        load()
    }

    private fun load() {
        val id = orderId
        if (id.isNullOrBlank()) {
            _state.value = BookingSuccessUiState.Error
            return
        }
        _state.value = BookingSuccessUiState.Loading
        viewModelScope.launch {
            val dto = try {
                val resp = orderApi.getById(id)
                if (resp.isSuccessful) resp.body() else null
            } catch (t: Throwable) {
                Log.w(TAG, "BookingSuccess: failed to fetch order $id", t)
                null
            }
            _state.value = if (dto != null) {
                BookingSuccessUiState.Loaded(dto)
            } else {
                Log.w(TAG, "BookingSuccess: empty order body for $id — degrading to generic success")
                BookingSuccessUiState.Error
            }
        }
    }

    private companion object {
        const val TAG = "BookingSuccessVM"
    }
}
