package cz.cleansia.partner.features.orders.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.R
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.NetworkMonitor
import cz.cleansia.partner.core.storage.PreferencesManager
import cz.cleansia.partner.core.utils.DateTimeUtils
import cz.cleansia.partner.domain.models.orders.Order
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.domain.models.orders.withStatus
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
import java.time.DayOfWeek
import java.time.LocalDate
import javax.inject.Inject

enum class OrderTab {
    AVAILABLE, MY_ORDERS
}

enum class OrdersViewMode {
    LIST, WEEK
}

enum class OrderSortOption(val displayNameResId: Int, val sortBy: String, val descending: Boolean) {
    DATE_ASC(R.string.sort_date_oldest, "cleaningDateTime", false),
    DATE_DESC(R.string.sort_date_newest, "cleaningDateTime", true),
    PRICE_ASC(R.string.sort_price_low_high, "totalPrice", false),
    PRICE_DESC(R.string.sort_price_high_low, "totalPrice", true)
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
    val pendingScrollToTop: Boolean = false,
    // Week view
    val viewMode: OrdersViewMode = OrdersViewMode.LIST,
    val selectedWeekDate: LocalDate = LocalDate.now(),
    val weekStartDate: LocalDate = LocalDate.now().with(DayOfWeek.MONDAY),
    // Offline mode
    val isOffline: Boolean = false,
    val lastCachedAt: Long? = null,
    val isShowingCachedData: Boolean = false,
    // Optimistic UI
    val isSyncing: Boolean = false
) {
    /** True when any of the employee's orders is currently IN_PROGRESS. */
    val hasOrderInProgress: Boolean
        get() = myOrders.any { it.status == OrderStatus.IN_PROGRESS }

    /** Orders filtered by selected day (for week view). */
    val filteredOrdersForSelectedDay: List<Order>
        get() {
            val orders = when (selectedTab) {
                OrderTab.AVAILABLE -> availableOrders
                OrderTab.MY_ORDERS -> myOrders
            }
            if (viewMode != OrdersViewMode.WEEK) return orders
            return orders.filter { order ->
                order.cleaningDateTime?.let { DateTimeUtils.parseToLocalDate(it) }?.isEqual(selectedWeekDate) == true
            }
        }

    /** Order count per day for the week strip dot indicators. */
    val ordersCountByDay: Map<LocalDate, Int>
        get() {
            val orders = when (selectedTab) {
                OrderTab.AVAILABLE -> availableOrders
                OrderTab.MY_ORDERS -> myOrders
            }
            return orders.mapNotNull { it.cleaningDateTime?.let { dt -> DateTimeUtils.parseToLocalDate(dt) } }
                .groupingBy { it }
                .eachCount()
        }
}

@HiltViewModel
class OrdersViewModel @Inject constructor(
    private val ordersRepository: OrdersRepository,
    private val preferencesManager: PreferencesManager,
    private val networkMonitor: NetworkMonitor,
    private val errorTranslator: ApiErrorTranslator
) : ViewModel() {

    private val _uiState = MutableStateFlow(OrdersUiState())
    val uiState: StateFlow<OrdersUiState> = _uiState.asStateFlow()

    private val filterManager = OrderFilterManager(
        stateUpdater = { update -> _uiState.update(update) },
        onRefresh = ::refresh
    )

    // Help card visibility (inverted from dismissed state)
    val showHelpCard: StateFlow<Boolean> = preferencesManager.ordersHelpDismissed
        .map { !it }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), true)

    init {
        observeNetworkState()
        loadOrders()
    }

    // ==================== Network / Offline ====================

    private fun observeNetworkState() {
        viewModelScope.launch {
            networkMonitor.isOnline.collect { isOnline ->
                _uiState.update { it.copy(isOffline = !isOnline) }
                if (!isOnline) {
                    loadCachedOrders()
                } else if (_uiState.value.isShowingCachedData) {
                    _uiState.update { it.copy(isShowingCachedData = false) }
                    refresh()
                }
            }
        }
    }

    private fun loadCachedOrders() {
        viewModelScope.launch {
            val cachedOrders = ordersRepository.getNext48HoursCachedOrders()
            val lastCached = ordersRepository.getLastCacheTimestamp()
            _uiState.update { state ->
                state.copy(
                    availableOrders = cachedOrders,
                    myOrders = cachedOrders,
                    isShowingCachedData = true,
                    lastCachedAt = lastCached,
                    isLoading = false,
                    isRefreshing = false
                )
            }
        }
    }

    // ==================== Week View ====================

    fun setViewMode(mode: OrdersViewMode) {
        _uiState.update { it.copy(viewMode = mode) }
    }

    fun selectWeekDate(date: LocalDate) {
        _uiState.update { it.copy(selectedWeekDate = date) }
    }

    fun navigateWeek(forward: Boolean) {
        _uiState.update { state ->
            val newStart = if (forward)
                state.weekStartDate.plusWeeks(1)
            else
                state.weekStartDate.minusWeeks(1)
            state.copy(
                weekStartDate = newStart,
                selectedWeekDate = newStart
            )
        }
    }

    /**
     * Called from outside to select a specific tab programmatically
     * (e.g. when navigating from dashboard stat cards).
     */
    fun selectTab(tab: OrderTab, statusFilter: OrderStatus? = null) {
        _uiState.update { state ->
            val newFilterState = if (statusFilter != null) {
                state.filterState.copy(orderStatuses = setOf(statusFilter))
            } else {
                state.filterState
            }
            state.copy(
                selectedTab = tab,
                filterState = newFilterState,
                pendingFilterState = newFilterState,
                pendingScrollToTop = true
            )
        }

        when (tab) {
            OrderTab.AVAILABLE -> loadAvailableOrders(reset = true)
            OrderTab.MY_ORDERS -> loadMyOrders(reset = true)
        }
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
                            error = errorTranslator.translateError(result.error),
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
                            error = errorTranslator.translateError(result.error),
                            isLoadingMore = false,
                            isRefreshing = false,
                            pendingScrollToTop = false
                        )
                    }
                }
            }
        }
    }

    suspend fun takeOrder(orderId: String): Boolean {
        val previousState = _uiState.value

        // Optimistic: move order from available to myOrders with CONFIRMED status
        _uiState.update { state ->
            val order = state.availableOrders.find { it.id == orderId } ?: return@update state
            val updatedOrder = order.withStatus(OrderStatus.CONFIRMED)
            state.copy(
                availableOrders = state.availableOrders.filter { it.id != orderId },
                myOrders = state.myOrders + updatedOrder,
                isSyncing = true
            )
        }

        return when (val result = ordersRepository.takeOrder(orderId)) {
            is ApiResult.Success -> {
                _uiState.update { it.copy(isSyncing = false) }
                refreshSilently()
                true
            }
            is ApiResult.Error -> {
                _uiState.value = previousState.copy(
                    error = errorTranslator.translateError(result.error),
                    isSyncing = false
                )
                false
            }
        }
    }

    suspend fun startOrder(orderId: String): Boolean {
        val previousState = _uiState.value

        // Optimistic: update status to IN_PROGRESS
        _uiState.update { state ->
            state.copy(
                myOrders = state.myOrders.map { order ->
                    if (order.id == orderId) order.withStatus(OrderStatus.IN_PROGRESS) else order
                },
                isSyncing = true
            )
        }

        return when (val result = ordersRepository.startOrder(orderId)) {
            is ApiResult.Success -> {
                _uiState.update { it.copy(isSyncing = false) }
                refreshSilently()
                true
            }
            is ApiResult.Error -> {
                _uiState.value = previousState.copy(
                    error = errorTranslator.translateError(result.error),
                    isSyncing = false
                )
                false
            }
        }
    }

    private fun refreshSilently() {
        viewModelScope.launch {
            when (_uiState.value.selectedTab) {
                OrderTab.AVAILABLE -> loadAvailableOrders(reset = true)
                OrderTab.MY_ORDERS -> loadMyOrders(reset = true)
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
    fun openFilterDrawer() = filterManager.openFilterDrawer()
    fun closeFilterDrawer() = filterManager.closeFilterDrawer()
    fun updateSearchTerm(term: String) = filterManager.updateSearchTerm(term)
    fun toggleOrderStatus(status: OrderStatus) = filterManager.toggleOrderStatus(status)
    fun togglePaymentStatus(status: cz.cleansia.partner.domain.models.orders.PaymentStatus) = filterManager.togglePaymentStatus(status)
    fun setStartDate(date: String?) = filterManager.setStartDate(date)
    fun setEndDate(date: String?) = filterManager.setEndDate(date)
    fun applyFilters() = filterManager.applyFilters()
    fun resetFilters() = filterManager.resetFilters()
    fun removeFilter(filterKey: String) = filterManager.removeFilter(filterKey)
}
