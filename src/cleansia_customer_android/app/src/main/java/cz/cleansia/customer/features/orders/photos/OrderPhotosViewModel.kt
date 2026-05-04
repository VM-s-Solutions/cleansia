package cz.cleansia.customer.features.orders.photos

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.core.orders.OrderPhotosResponse
import cz.cleansia.customer.core.orders.OrderRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * ViewModel for the [OrderPhotosScreen] — fetches the before/after photo list
 * for a single order and exposes it as a Loading → Loaded/Error funnel.
 *
 * Photos are fetched fresh every time the screen opens — the Azure SAS URLs
 * embedded in `blobUrl` expire after 1h, so caching them in the repository
 * would risk serving stale signed URLs. [OrderRepository.getPhotos] already
 * surfaces a snackbar on failure, so we just translate a null result into
 * [UiState.Error] and let the screen render a retry affordance.
 */
@HiltViewModel
class OrderPhotosViewModel @Inject constructor(
    private val orderRepository: OrderRepository,
    savedStateHandle: SavedStateHandle,
) : ViewModel() {

    sealed interface UiState {
        data object Loading : UiState
        data class Loaded(val response: OrderPhotosResponse) : UiState
        data object Error : UiState
    }

    /** Captured once — the nav arg never changes for a given screen instance. */
    private val orderId: String? = savedStateHandle.get<String>("orderId")

    private val _state = MutableStateFlow<UiState>(UiState.Loading)
    val state: StateFlow<UiState> = _state.asStateFlow()

    init {
        refresh()
    }

    /** Public retry hook — called from the Error surface's Retry button. */
    fun refresh() {
        val id = orderId
        if (id.isNullOrBlank()) {
            _state.value = UiState.Error
            return
        }
        viewModelScope.launch {
            _state.value = UiState.Loading
            val resp = orderRepository.getPhotos(id)
            _state.value = if (resp != null) UiState.Loaded(resp) else UiState.Error
        }
    }
}
