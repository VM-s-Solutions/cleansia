package cz.cleansia.partner.features.invoices.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.R
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.storage.PreferencesManager
import cz.cleansia.partner.domain.models.invoices.Invoice
import cz.cleansia.partner.domain.models.invoices.InvoiceFilter
import cz.cleansia.partner.domain.models.invoices.InvoiceStatus
import cz.cleansia.partner.domain.repositories.InvoicesRepository
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

enum class InvoiceSortOption(val displayNameResId: Int, val sortBy: String, val descending: Boolean) {
    DATE_DESC(R.string.sort_date_newest, "generatedAt", true),
    DATE_ASC(R.string.sort_date_oldest, "generatedAt", false),
    AMOUNT_DESC(R.string.sort_amount_high_low, "totalAmount", true),
    AMOUNT_ASC(R.string.sort_amount_low_high, "totalAmount", false)
}

/**
 * Filter state for invoices
 */
data class InvoiceFilterState(
    val searchTerm: String = "",
    val invoiceStatuses: Set<InvoiceStatus> = emptySet(),
    val startDate: String? = null,
    val endDate: String? = null
) {
    val activeFilterCount: Int
        get() = listOfNotNull(
            searchTerm.takeIf { it.isNotBlank() },
            invoiceStatuses.takeIf { it.isNotEmpty() },
            startDate,
            endDate
        ).size

    val hasActiveFilters: Boolean get() = activeFilterCount > 0
}

data class InvoicesUiState(
    val isLoading: Boolean = false,
    val isRefreshing: Boolean = false,
    val isLoadingMore: Boolean = false,
    val error: String? = null,
    val invoices: List<Invoice> = emptyList(),
    val hasMore: Boolean = false,
    val currentPage: Int = 1,
    val sortOption: InvoiceSortOption = InvoiceSortOption.DATE_DESC,
    val scrollToTop: Boolean = false,
    val pendingScrollToTop: Boolean = false,
    val showSortMenu: Boolean = false,
    val isFilterDrawerOpen: Boolean = false,
    val filterState: InvoiceFilterState = InvoiceFilterState(),
    val pendingFilterState: InvoiceFilterState = InvoiceFilterState()
)

@HiltViewModel
class InvoicesViewModel @Inject constructor(
    private val invoicesRepository: InvoicesRepository,
    private val preferencesManager: PreferencesManager,
    private val errorTranslator: ApiErrorTranslator
) : ViewModel() {

    private val _uiState = MutableStateFlow(InvoicesUiState())
    val uiState: StateFlow<InvoicesUiState> = _uiState.asStateFlow()

    // Help card visibility (inverted from dismissed state)
    val showHelpCard: StateFlow<Boolean> = preferencesManager.invoicesHelpDismissed
        .map { !it }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), true)

    init {
        loadInvoices()
    }

    fun dismissHelpCard() {
        viewModelScope.launch {
            preferencesManager.setInvoicesHelpDismissed(true)
        }
    }

    private fun buildFilter(): InvoiceFilter? {
        val state = _uiState.value.filterState
        if (!state.hasActiveFilters) return null
        return InvoiceFilter(
            statuses = state.invoiceStatuses.toList(),
            searchTerm = state.searchTerm.takeIf { it.isNotBlank() },
            startDate = state.startDate,
            endDate = state.endDate
        )
    }

    fun loadInvoices() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            val state = _uiState.value

            when (val result = invoicesRepository.getInvoices(
                page = 1,
                filter = buildFilter(),
                sortBy = state.sortOption.sortBy,
                sortDescending = state.sortOption.descending
            )) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            invoices = result.data.items,
                            hasMore = result.data.hasNextPage,
                            currentPage = 1,
                            scrollToTop = it.pendingScrollToTop,
                            pendingScrollToTop = false
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            error = errorTranslator.translateError(result.error),
                            pendingScrollToTop = false
                        )
                    }
                }
            }
        }
    }

    fun refresh() {
        viewModelScope.launch {
            _uiState.update { it.copy(isRefreshing = true, error = null) }
            val state = _uiState.value

            when (val result = invoicesRepository.getInvoices(
                page = 1,
                filter = buildFilter(),
                sortBy = state.sortOption.sortBy,
                sortDescending = state.sortOption.descending
            )) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isRefreshing = false,
                            invoices = result.data.items,
                            hasMore = result.data.hasNextPage,
                            currentPage = 1,
                            scrollToTop = it.pendingScrollToTop,
                            pendingScrollToTop = false
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isRefreshing = false,
                            error = errorTranslator.translateError(result.error),
                            pendingScrollToTop = false
                        )
                    }
                }
            }
        }
    }

    fun loadMore() {
        val state = _uiState.value
        if (state.isLoadingMore || !state.hasMore) return

        viewModelScope.launch {
            _uiState.update { it.copy(isLoadingMore = true) }

            val nextPage = state.currentPage + 1
            when (val result = invoicesRepository.getInvoices(
                page = nextPage,
                filter = buildFilter(),
                sortBy = state.sortOption.sortBy,
                sortDescending = state.sortOption.descending
            )) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isLoadingMore = false,
                            invoices = it.invoices + result.data.items,
                            hasMore = result.data.hasNextPage,
                            currentPage = nextPage
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isLoadingMore = false,
                            error = errorTranslator.translateError(result.error)
                        )
                    }
                }
            }
        }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null) }
    }

    // Sort menu methods
    fun showSortMenu() {
        _uiState.update { it.copy(showSortMenu = true) }
    }

    fun hideSortMenu() {
        _uiState.update { it.copy(showSortMenu = false) }
    }

    fun setSortOption(option: InvoiceSortOption) {
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

    fun toggleInvoiceStatus(status: InvoiceStatus) {
        _uiState.update { state ->
            val currentStatuses = state.pendingFilterState.invoiceStatuses.toMutableSet()
            if (currentStatuses.contains(status)) {
                currentStatuses.remove(status)
            } else {
                currentStatuses.add(status)
            }
            state.copy(pendingFilterState = state.pendingFilterState.copy(invoiceStatuses = currentStatuses))
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
        _uiState.update { it.copy(filterState = InvoiceFilterState(), pendingFilterState = InvoiceFilterState(), pendingScrollToTop = true) }
        refresh()
    }

    fun removeFilter(filterKey: String) {
        _uiState.update { state ->
            val newFilterState = when (filterKey) {
                "search" -> state.filterState.copy(searchTerm = "")
                "invoiceStatus" -> state.filterState.copy(invoiceStatuses = emptySet())
                "startDate" -> state.filterState.copy(startDate = null)
                "endDate" -> state.filterState.copy(endDate = null)
                else -> state.filterState
            }
            state.copy(filterState = newFilterState, pendingFilterState = newFilterState, pendingScrollToTop = true)
        }
        refresh()
    }
}
