package cz.cleansia.customer.features.booking

import android.util.Log
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
 * The server's order behind [BookingSuccessScreen]: it confirms the submitted appointment and address and is
 * the only source of the reference, the free-cancellation window, the total and the payment lines. On [Error]
 * the sheet shows Total with a dash and the "temporarily unavailable" line with Try again; the appointment
 * stays as submitted.
 */
sealed interface BookingSuccessUiState {
    data object Loading : BookingSuccessUiState
    data class Loaded(val order: OrderDetailDto) : BookingSuccessUiState
    data object Error : BookingSuccessUiState
}

/**
 * Fetches the newly-created order's detail for the success screen when the
 * sheet starts it, and again on [retry] after a failure, so the user sees
 * their arrival window, address and total without having to bounce to the
 * Orders tab.
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
) : ViewModel() {

    private var startedFor: String? = null

    private val _state = MutableStateFlow<BookingSuccessUiState>(BookingSuccessUiState.Loading)
    val state: StateFlow<BookingSuccessUiState> = _state.asStateFlow()

    init {
        // Pre-warm the Orders list so the new order is visible the moment the
        // user lands on the Orders tab. Failure is silent — the tab refreshes
        // itself when it's empty.
        viewModelScope.launch {
            orderRepository.refresh()
        }
    }

    fun start(orderId: String) {
        if (startedFor == orderId) return
        startedFor = orderId
        load(orderId)
    }

    fun retry() {
        val id = startedFor ?: return
        load(id)
    }

    private fun load(id: String) {
        if (id.isBlank()) {
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
