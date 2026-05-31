package cz.cleansia.partner.features.orders.viewmodels

import cz.cleansia.partner.domain.models.orders.OrderStatus

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

class OrderFilterManager(
    private val stateUpdater: (update: (OrdersUiState) -> OrdersUiState) -> Unit,
    private val onRefresh: () -> Unit
) {
    fun openFilterDrawer() {
        stateUpdater { it.copy(isFilterDrawerOpen = true, pendingFilterState = it.filterState) }
    }

    fun closeFilterDrawer() {
        stateUpdater { it.copy(isFilterDrawerOpen = false) }
    }

    fun updateSearchTerm(term: String) {
        stateUpdater { it.copy(pendingFilterState = it.pendingFilterState.copy(searchTerm = term)) }
    }

    fun toggleOrderStatus(status: OrderStatus) {
        stateUpdater { state ->
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
        stateUpdater { state ->
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
        stateUpdater { it.copy(pendingFilterState = it.pendingFilterState.copy(startDate = date)) }
    }

    fun setEndDate(date: String?) {
        stateUpdater { it.copy(pendingFilterState = it.pendingFilterState.copy(endDate = date)) }
    }

    fun applyFilters() {
        stateUpdater { it.copy(filterState = it.pendingFilterState, pendingScrollToTop = true) }
        closeFilterDrawer()
        onRefresh()
    }

    fun resetFilters() {
        stateUpdater { it.copy(filterState = OrderFilterState(), pendingFilterState = OrderFilterState(), pendingScrollToTop = true) }
        onRefresh()
    }

    fun removeFilter(filterKey: String) {
        stateUpdater { state ->
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
        onRefresh()
    }
}
