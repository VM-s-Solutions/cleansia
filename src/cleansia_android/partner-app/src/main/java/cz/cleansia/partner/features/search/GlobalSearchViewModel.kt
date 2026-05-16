package cz.cleansia.partner.features.search

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.invoices.Invoice
import cz.cleansia.partner.domain.models.invoices.InvoiceFilter
import cz.cleansia.partner.domain.models.orders.Order
import cz.cleansia.partner.domain.repositories.InvoicesRepository
import cz.cleansia.partner.domain.repositories.OrdersRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class GlobalSearchState(
    val query: String = "",
    val isSearching: Boolean = false,
    val orderResults: List<Order> = emptyList(),
    val invoiceResults: List<Invoice> = emptyList(),
    val isActive: Boolean = false
)

@HiltViewModel
class GlobalSearchViewModel @Inject constructor(
    private val ordersRepository: OrdersRepository,
    private val invoicesRepository: InvoicesRepository
) : ViewModel() {

    private val _state = MutableStateFlow(GlobalSearchState())
    val state: StateFlow<GlobalSearchState> = _state.asStateFlow()

    private var searchJob: Job? = null

    fun updateQuery(query: String) {
        _state.update { it.copy(query = query) }
        if (query.length >= 2) {
            searchJob?.cancel()
            searchJob = viewModelScope.launch {
                delay(350)
                performSearch(query)
            }
        } else {
            _state.update { it.copy(orderResults = emptyList(), invoiceResults = emptyList()) }
        }
    }

    fun setActive(active: Boolean) {
        _state.update { it.copy(isActive = active) }
        if (!active) {
            clearSearch()
        }
    }

    fun clearSearch() {
        searchJob?.cancel()
        _state.update {
            GlobalSearchState()
        }
    }

    private suspend fun performSearch(query: String) {
        _state.update { it.copy(isSearching = true, orderResults = emptyList()) }

        // Search my orders by customer name
        val myOrdersJob = viewModelScope.launch {
            try {
                val result = ordersRepository.getMyOrders(
                    page = 1,
                    statuses = emptyList(),
                    searchTerm = query,
                    sortBy = "cleaningDateTime",
                    sortDescending = true
                )
                if (result is ApiResult.Success) {
                    _state.update { current ->
                        val merged = mergeOrders(current.orderResults, result.data.orders)
                        current.copy(orderResults = merged.take(5))
                    }
                }
            } catch (_: Exception) { }
        }

        // Search available orders by customer name
        val availableOrdersJob = viewModelScope.launch {
            try {
                val result = ordersRepository.getAvailableOrders(
                    page = 1,
                    searchTerm = query,
                    sortBy = "cleaningDateTime",
                    sortDescending = true
                )
                if (result is ApiResult.Success) {
                    _state.update { current ->
                        val merged = mergeOrders(current.orderResults, result.data.orders)
                        current.copy(orderResults = merged.take(5))
                    }
                }
            } catch (_: Exception) { }
        }

        // Search orders by order number (display number)
        val orderNumberJob = viewModelScope.launch {
            try {
                val result = ordersRepository.searchOrdersByNumber(
                    page = 1,
                    orderNumber = query
                )
                if (result is ApiResult.Success) {
                    _state.update { current ->
                        val merged = mergeOrders(current.orderResults, result.data.orders)
                        current.copy(orderResults = merged.take(5))
                    }
                }
            } catch (_: Exception) { }
        }

        val invoicesJob = viewModelScope.launch {
            try {
                val result = invoicesRepository.getInvoices(
                    page = 1,
                    pageSize = 5,
                    filter = InvoiceFilter(searchTerm = query)
                )
                if (result is ApiResult.Success) {
                    _state.update { it.copy(invoiceResults = result.data.items.take(5)) }
                }
            } catch (_: Exception) { }
        }

        myOrdersJob.join()
        availableOrdersJob.join()
        orderNumberJob.join()
        invoicesJob.join()

        _state.update { it.copy(isSearching = false) }
    }

    private fun mergeOrders(existing: List<Order>, newOrders: List<Order>): List<Order> {
        val existingIds = existing.map { it.id }.toSet()
        return existing + newOrders.filter { it.id !in existingIds }
    }
}
