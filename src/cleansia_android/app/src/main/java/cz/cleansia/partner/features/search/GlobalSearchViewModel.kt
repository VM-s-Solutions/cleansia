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
        _state.update { it.copy(isSearching = true) }

        val ordersDeferred = viewModelScope.launch {
            when (val result = ordersRepository.getMyOrders(
                page = 1,
                statuses = emptyList(),
                searchTerm = query,
                sortBy = "cleaningDateTime",
                sortDescending = true
            )) {
                is ApiResult.Success -> {
                    _state.update { it.copy(orderResults = result.data.orders.take(3)) }
                }
                is ApiResult.Error -> { /* silently fail for search */ }
            }
        }

        val invoicesDeferred = viewModelScope.launch {
            when (val result = invoicesRepository.getInvoices(
                page = 1,
                pageSize = 3,
                filter = InvoiceFilter(searchTerm = query)
            )) {
                is ApiResult.Success -> {
                    _state.update { it.copy(invoiceResults = result.data.items.take(3)) }
                }
                is ApiResult.Error -> { /* silently fail for search */ }
            }
        }

        ordersDeferred.join()
        invoicesDeferred.join()

        _state.update { it.copy(isSearching = false) }
    }
}
