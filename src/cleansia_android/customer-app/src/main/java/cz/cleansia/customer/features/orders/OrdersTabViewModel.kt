package cz.cleansia.customer.features.orders

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.orders.OrderListItemDto
import cz.cleansia.customer.core.orders.OrderRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch

/** Injection seam for the orders tab's singleton repository. No state lives here. */
@HiltViewModel
class OrdersTabViewModel @Inject constructor(
    private val orderRepository: OrderRepository,
    private val snackbar: SnackbarController,
) : ViewModel() {

    val orders: StateFlow<List<OrderListItemDto>> = orderRepository.orders
    val loading: StateFlow<Boolean> = orderRepository.loading
    val loadingMore: StateFlow<Boolean> = orderRepository.loadingMore
    val loaded: StateFlow<Boolean> = orderRepository.loaded
    val totalRecords: StateFlow<Int> = orderRepository.totalRecords

    fun refresh() {
        viewModelScope.launch {
            orderRepository.refresh().onError { error ->
                if (error !is ApiError.Network) snackbar.showError(error)
            }
        }
    }

    /** Tab entry: a refresh already in flight is left to land rather than stacked. */
    fun refreshUnlessLoading() {
        if (!orderRepository.loading.value) refresh()
    }

    fun loadNextPage() {
        viewModelScope.launch { orderRepository.loadNextPage() }
    }
}
