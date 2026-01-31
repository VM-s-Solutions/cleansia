package cz.cleansia.partner.features.orders.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.R
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.storage.PreferencesManager
import cz.cleansia.partner.domain.models.orders.Order
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.domain.repositories.OrdersRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

enum class OrderTab {
    AVAILABLE, MY_ORDERS
}

enum class OrderSortOption(val displayNameResId: Int, val sortBy: String, val descending: Boolean) {
    DATE_ASC(R.string.sort_date_oldest, "cleaningDateTime", false),
    DATE_DESC(R.string.sort_date_newest, "cleaningDateTime", true),
    PRICE_ASC(R.string.sort_price_low_high, "totalPrice", false),
    PRICE_DESC(R.string.sort_price_high_low, "totalPrice", true)
}

/**
 * Filter state for orders
 */
data class OrderFilterState(
    val searchTerm: String = "",
    val orderStatuses: Set<OrderStatus> = emptySet(),
    val paymentStatuses: Set<cz.cleansia.partner.domain.models.orders.PaymentStatus> = emptySet(),
    val startDate: String? = null,
    val endDate: String? = null
) {
    val activeFilterCount: Int
        get() = listOfNotNull(
            searchTerm.takeIf { it.isNotBlank() },
            orderStatuses.takeIf { it.isNotEmpty() },
            paymentStatuses.takeIf { it.isNotEmpty() },
            startDate,
            endDate
        ).size

    val hasActiveFilters: Boolean get() = activeFilterCount > 0
}

data class OrdersUiState(
    val selectedTab: OrderTab = OrderTab.AVAILABLE,
    val isLoading: Boolean = false,
    val isRefreshing: Boolean = false,
    val isLoadingMore: Boolean = false,
    val error: String? = null,
    val availableOrders: List<Order> = emptyList(),
    val myOrders: List<Order> = emptyList(),
    val hasMoreAvailable: Boolean = false,
    val hasMoreMyOrders: Boolean = false,
    val availablePage: Int = 1,
    val myOrdersPage: Int = 1,
    val sortOption: OrderSortOption = OrderSortOption.DATE_DESC,
    val showSortMenu: Boolean = false,
    val isFilterDrawerOpen: Boolean = false,
    val filterState: OrderFilterState = OrderFilterState(),
    val pendingFilterState: OrderFilterState = OrderFilterState(),
    val scrollToTop: Boolean = false,
    val pendingScrollToTop: Boolean = false
)

@HiltViewModel
class OrdersViewModel @Inject constructor(
    private val ordersRepository: OrdersRepository,
    private val preferencesManager: PreferencesManager
) : ViewModel() {

    private val _uiState = MutableStateFlow(OrdersUiState())
    val uiState: StateFlow<OrdersUiState> = _uiState.asStateFlow()

    // Help card visibility (inverted from dismissed state)
    val showHelpCard: StateFlow<Boolean> = preferencesManager.ordersHelpDismissed
        .map { !it }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), true)

    init {
        loadOrders()
    }

    fun dismissHelpCard() {
        viewModelScope.launch {
            preferencesManager.setOrdersHelpDismissed(true)
        }
    }

    fun onTabSelected(tab: OrderTab) {
        _uiState.update { it.copy(selectedTab = tab) }

        // Load data for the selected tab if empty
        val state = _uiState.value
        when (tab) {
            OrderTab.AVAILABLE -> if (state.availableOrders.isEmpty()) loadAvailableOrders()
            OrderTab.MY_ORDERS -> if (state.myOrders.isEmpty()) loadMyOrders()
        }
    }

    fun loadOrders() {
        _uiState.update { it.copy(isLoading = true, error = null) }
        loadAvailableOrders(initialLoad = true)
    }

    fun refresh() {
        _uiState.update {
            it.copy(
                isRefreshing = true,
                error = null,
                availablePage = 1,
                myOrdersPage = 1
            )
        }

        when (_uiState.value.selectedTab) {
            OrderTab.AVAILABLE -> loadAvailableOrders(reset = true)
            OrderTab.MY_ORDERS -> loadMyOrders(reset = true)
        }
    }

    fun loadMore() {
        val state = _uiState.value
        if (state.isLoadingMore) return

        when (state.selectedTab) {
            OrderTab.AVAILABLE -> if (state.hasMoreAvailable) loadAvailableOrders(loadMore = true)
            OrderTab.MY_ORDERS -> if (state.hasMoreMyOrders) loadMyOrders(loadMore = true)
        }
    }

    private fun loadAvailableOrders(reset: Boolean = false, loadMore: Boolean = false, initialLoad: Boolean = false) {
        viewModelScope.launch {
            if (loadMore) {
                _uiState.update { it.copy(isLoadingMore = true) }
            }

            val state = _uiState.value
            val page = if (reset || initialLoad) 1 else if (loadMore) state.availablePage + 1 else 1

            when (val result = ordersRepository.getAvailableOrders(
                page = page,
                searchTerm = state.filterState.searchTerm.takeIf { it.isNotBlank() },
                startDate = state.filterState.startDate,
                endDate = state.filterState.endDate,
                sortBy = state.sortOption.sortBy,
                sortDescending = state.sortOption.descending,
                paymentStatuses = state.filterState.paymentStatuses.toList()
            )) {
                is ApiResult.Success -> {
                    val orders = result.data.orders
                    _uiState.update { s ->
                        s.copy(
                            availableOrders = if (reset || initialLoad || !loadMore) orders else s.availableOrders + orders,
                            hasMoreAvailable = result.data.hasMore,
                            availablePage = page,
                            isLoadingMore = false,
                            isRefreshing = false,
                            isLoading = false,
                            scrollToTop = s.pendingScrollToTop,
                            pendingScrollToTop = false
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            error = result.error.getUserMessage(),
                            isLoadingMore = false,
                            isRefreshing = false,
                            isLoading = false,
                            pendingScrollToTop = false
                        )
                    }
                }
            }
        }
    }

    private fun loadMyOrders(reset: Boolean = false, loadMore: Boolean = false) {
        viewModelScope.launch {
            if (loadMore) {
                _uiState.update { it.copy(isLoadingMore = true) }
            }

            val state = _uiState.value
            val page = if (reset) 1 else if (loadMore) state.myOrdersPage + 1 else 1

            when (val result = ordersRepository.getMyOrders(
                page = page,
                statuses = state.filterState.orderStatuses.toList(),
                searchTerm = state.filterState.searchTerm.takeIf { it.isNotBlank() },
                startDate = state.filterState.startDate,
                endDate = state.filterState.endDate,
                sortBy = state.sortOption.sortBy,
                sortDescending = state.sortOption.descending,
                paymentStatuses = state.filterState.paymentStatuses.toList()
            )) {
                is ApiResult.Success -> {
                    val orders = result.data.orders
                    _uiState.update { s ->
                        s.copy(
                            myOrders = if (reset || !loadMore) orders else s.myOrders + orders,
                            hasMoreMyOrders = result.data.hasMore,
                            myOrdersPage = page,
                            isLoadingMore = false,
                            isRefreshing = false,
                            scrollToTop = s.pendingScrollToTop,
                            pendingScrollToTop = false
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            error = result.error.getUserMessage(),
                            isLoadingMore = false,
                            isRefreshing = false,
                            pendingScrollToTop = false
                        )
                    }
                }
            }
        }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null) }
    }

    fun showSortMenu() {
        _uiState.update { it.copy(showSortMenu = true) }
    }

    fun hideSortMenu() {
        _uiState.update { it.copy(showSortMenu = false) }
    }

    fun setSortOption(option: OrderSortOption) {
        _uiState.update { it.copy(sortOption = option, showSortMenu = false, pendingScrollToTop = true) }
        refresh()
    }

    fun consumeScrollToTop() {
        _uiState.update { it.copy(scrollToTop = false) }
    }

    // Filter drawer methods
    fun openFilterDrawer() {
        _uiState.update { it.copy(isFilterDrawerOpen = true, pendingFilterState = it.filterState) }
    }

    fun closeFilterDrawer() {
        _uiState.update { it.copy(isFilterDrawerOpen = false) }
    }

    fun updateSearchTerm(term: String) {
        _uiState.update { it.copy(pendingFilterState = it.pendingFilterState.copy(searchTerm = term)) }
    }

    fun toggleOrderStatus(status: OrderStatus) {
        _uiState.update { state ->
            val currentStatuses = state.pendingFilterState.orderStatuses.toMutableSet()
            if (currentStatuses.contains(status)) {
                currentStatuses.remove(status)
            } else {
                currentStatuses.add(status)
            }
            state.copy(pendingFilterState = state.pendingFilterState.copy(orderStatuses = currentStatuses))
        }
    }

    fun togglePaymentStatus(status: cz.cleansia.partner.domain.models.orders.PaymentStatus) {
        _uiState.update { state ->
            val currentStatuses = state.pendingFilterState.paymentStatuses.toMutableSet()
            if (currentStatuses.contains(status)) {
                currentStatuses.remove(status)
            } else {
                currentStatuses.add(status)
            }
            state.copy(pendingFilterState = state.pendingFilterState.copy(paymentStatuses = currentStatuses))
        }
    }

    fun setStartDate(date: String?) {
        _uiState.update { it.copy(pendingFilterState = it.pendingFilterState.copy(startDate = date)) }
    }

    fun setEndDate(date: String?) {
        _uiState.update { it.copy(pendingFilterState = it.pendingFilterState.copy(endDate = date)) }
    }

    fun applyFilters() {
        _uiState.update { it.copy(filterState = it.pendingFilterState, pendingScrollToTop = true) }
        closeFilterDrawer()
        refresh()
    }

    fun resetFilters() {
        _uiState.update { it.copy(filterState = OrderFilterState(), pendingFilterState = OrderFilterState(), pendingScrollToTop = true) }
        refresh()
    }

    fun removeFilter(filterKey: String) {
        _uiState.update { state ->
            val newFilterState = when (filterKey) {
                "search" -> state.filterState.copy(searchTerm = "")
                "orderStatus" -> state.filterState.copy(orderStatuses = emptySet())
                "paymentStatus" -> state.filterState.copy(paymentStatuses = emptySet())
                "startDate" -> state.filterState.copy(startDate = null)
                "endDate" -> state.filterState.copy(endDate = null)
                else -> state.filterState
            }
            state.copy(filterState = newFilterState, pendingFilterState = newFilterState, pendingScrollToTop = true)
        }
        refresh()
    }
}
