package cz.cleansia.partner.features.invoices.screens

import androidx.compose.foundation.clickable
import cz.cleansia.partner.ui.components.clickableWithHaptic
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.ui.components.CleansiaSnackbarHost
import cz.cleansia.partner.domain.models.invoices.Invoice
import cz.cleansia.partner.domain.models.invoices.InvoiceStatus
import cz.cleansia.partner.features.invoices.components.InvoicesFilterContent
import cz.cleansia.partner.features.invoices.viewmodels.InvoiceFilterState
import cz.cleansia.partner.features.invoices.viewmodels.InvoiceSortOption
import cz.cleansia.partner.features.invoices.viewmodels.InvoicesViewModel
import cz.cleansia.partner.ui.components.ActiveFilterChipsBar
import cz.cleansia.partner.ui.components.ErrorView
import cz.cleansia.partner.ui.components.FilterButton
import cz.cleansia.partner.ui.components.FilterChip
import cz.cleansia.partner.ui.components.FilterBottomSheet
import cz.cleansia.partner.ui.components.InfoHelpCard
import cz.cleansia.partner.ui.components.InvoiceStatusBadge
import cz.cleansia.partner.features.invoices.components.InvoicesListSkeleton
import cz.cleansia.partner.ui.components.LoadingIndicator
import cz.cleansia.partner.ui.components.SortButton
import cz.cleansia.partner.core.utils.DateTimeUtils
import java.text.NumberFormat
import java.util.Currency
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun InvoicesScreen(
    onNavigateToInvoiceDetails: (String) -> Unit,
    onScrolled: (Boolean) -> Unit = {},
    listState: LazyListState = rememberLazyListState(),
    viewModel: InvoicesViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val showHelpCard by viewModel.showHelpCard.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    // Show error in snackbar
    LaunchedEffect(uiState.error) {
        uiState.error?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    // Build active filter chips
    val activeFilterChips = buildActiveFilterChips(uiState.filterState)

    Box(modifier = Modifier.fillMaxSize()) {
        Scaffold { paddingValues ->
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .padding(top = 64.dp)
            ) {
                // Filter and Sort buttons row
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp, vertical = 8.dp),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    FilterButton(
                        activeFilterCount = uiState.filterState.activeFilterCount,
                        onClick = { viewModel.openFilterDrawer() }
                    )
                    Box {
                        SortButton(
                            currentSortLabel = stringResource(uiState.sortOption.displayNameResId),
                            onClick = { viewModel.showSortMenu() }
                        )
                        SortDropdownMenu(
                            expanded = uiState.showSortMenu,
                            currentSort = uiState.sortOption,
                            onDismiss = { viewModel.hideSortMenu() },
                            onSortSelected = { viewModel.setSortOption(it) }
                        )
                    }
                }

                // Active filter chips bar
                if (activeFilterChips.isNotEmpty()) {
                    ActiveFilterChipsBar(
                        chips = activeFilterChips,
                        onRemoveChip = { viewModel.removeFilter(it) },
                        onClearAll = { viewModel.resetFilters() }
                    )
                }

                // Help card
                InfoHelpCard(
                    title = stringResource(R.string.invoices_help_title),
                    description = stringResource(R.string.invoices_help_desc),
                    isVisible = showHelpCard,
                    onDismiss = { viewModel.dismissHelpCard() },
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)
                )

                when {
                    uiState.isLoading -> {
                        InvoicesListSkeleton(modifier = Modifier.fillMaxSize())
                    }
                    uiState.error != null && uiState.invoices.isEmpty() -> {
                        ErrorView(
                            message = uiState.error ?: "Unknown error",
                            onRetry = { viewModel.loadInvoices() },
                            modifier = Modifier.fillMaxSize()
                        )
                    }
                    else -> {
                        PullToRefreshBox(
                            isRefreshing = uiState.isRefreshing,
                            onRefresh = { viewModel.refresh() },
                            modifier = Modifier.fillMaxSize()
                        ) {
                            if (uiState.invoices.isEmpty()) {
                                EmptyInvoicesView()
                            } else {
                                InvoicesList(
                                    invoices = uiState.invoices,
                                    hasMore = uiState.hasMore,
                                    isLoadingMore = uiState.isLoadingMore,
                                    scrollToTop = uiState.scrollToTop,
                                    onScrollToTopConsumed = { viewModel.consumeScrollToTop() },
                                    onInvoiceClick = onNavigateToInvoiceDetails,
                                    onLoadMore = { viewModel.loadMore() },
                                    onScrolled = onScrolled,
                                    listState = listState
                                )
                            }
                        }
                    }
                }
            }
        }

        // Filter Bottom Sheet
        FilterBottomSheet(
            isOpen = uiState.isFilterDrawerOpen,
            onDismiss = { viewModel.closeFilterDrawer() },
            onReset = { viewModel.resetFilters() },
            onApply = { viewModel.applyFilters() }
        ) {
            InvoicesFilterContent(
                filterState = uiState.pendingFilterState,
                onSearchTermChange = { viewModel.updateSearchTerm(it) },
                onInvoiceStatusToggle = { viewModel.toggleInvoiceStatus(it) },
                onStartDateChange = { viewModel.setStartDate(it) },
                onEndDateChange = { viewModel.setEndDate(it) }
            )
        }

        CleansiaSnackbarHost(hostState = snackbarHostState)
    }
}

@Composable
private fun SortDropdownMenu(
    expanded: Boolean,
    currentSort: InvoiceSortOption,
    onDismiss: () -> Unit,
    onSortSelected: (InvoiceSortOption) -> Unit
) {
    DropdownMenu(
        expanded = expanded,
        onDismissRequest = onDismiss
    ) {
        InvoiceSortOption.entries.forEach { option ->
            DropdownMenuItem(
                text = {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Text(text = stringResource(option.displayNameResId))
                        if (option == currentSort) {
                            Icon(
                                imageVector = Icons.Default.Check,
                                contentDescription = null,
                                modifier = Modifier.size(16.dp),
                                tint = MaterialTheme.colorScheme.primary
                            )
                        }
                    }
                },
                onClick = { onSortSelected(option) }
            )
        }
    }
}

/**
 * Build active filter chips from filter state
 */
@Composable
private fun buildActiveFilterChips(filterState: InvoiceFilterState): List<FilterChip> {
    val chips = mutableListOf<FilterChip>()

    // Pre-resolve all status display names
    val statusNameMap = mapOf(
        InvoiceStatus.PENDING to stringResource(R.string.invoice_pending),
        InvoiceStatus.APPROVED to stringResource(R.string.invoice_approved),
        InvoiceStatus.PAID to stringResource(R.string.invoice_paid),
        InvoiceStatus.DISPUTED to stringResource(R.string.invoice_disputed),
        InvoiceStatus.REJECTED to stringResource(R.string.invoice_rejected),
        InvoiceStatus.CANCELLED to stringResource(R.string.invoice_cancelled)
    )

    if (filterState.searchTerm.isNotBlank()) {
        chips.add(FilterChip("search", stringResource(R.string.search), filterState.searchTerm))
    }

    if (filterState.invoiceStatuses.isNotEmpty()) {
        val statusNames = filterState.invoiceStatuses.joinToString(", ") { statusNameMap[it] ?: "" }
        chips.add(FilterChip("invoiceStatus", stringResource(R.string.invoice_status_filter), statusNames))
    }

    filterState.startDate?.let {
        chips.add(FilterChip("startDate", stringResource(R.string.start_date), DateTimeUtils.formatDate(it)))
    }

    filterState.endDate?.let {
        chips.add(FilterChip("endDate", stringResource(R.string.end_date), DateTimeUtils.formatDate(it)))
    }

    return chips
}

@Composable
private fun InvoicesList(
    invoices: List<Invoice>,
    hasMore: Boolean,
    isLoadingMore: Boolean,
    scrollToTop: Boolean = false,
    onScrollToTopConsumed: () -> Unit = {},
    onInvoiceClick: (String) -> Unit,
    onLoadMore: () -> Unit,
    onScrolled: (Boolean) -> Unit = {},
    listState: LazyListState = rememberLazyListState()
) {

    // Report scroll state to parent
    val isScrolled by remember {
        derivedStateOf { listState.firstVisibleItemIndex > 0 || listState.firstVisibleItemScrollOffset > 0 }
    }
    LaunchedEffect(isScrolled) {
        onScrolled(isScrolled)
    }

    // Scroll to top when triggered
    LaunchedEffect(scrollToTop) {
        if (scrollToTop) {
            listState.animateScrollToItem(0)
            onScrollToTopConsumed()
        }
    }

    // Detect when we're near the end of the list
    val shouldLoadMore by remember {
        derivedStateOf {
            val lastVisibleItem = listState.layoutInfo.visibleItemsInfo.lastOrNull()
            lastVisibleItem != null && lastVisibleItem.index >= invoices.size - 3
        }
    }

    LaunchedEffect(shouldLoadMore) {
        if (shouldLoadMore && hasMore && !isLoadingMore) {
            onLoadMore()
        }
    }

    LazyColumn(
        state = listState,
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 16.dp, bottom = 100.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        items(invoices, key = { it.id }) { invoice ->
            InvoiceCard(
                invoice = invoice,
                onClick = { onInvoiceClick(invoice.id) }
            )
        }

        if (isLoadingMore) {
            item {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    contentAlignment = Alignment.Center
                ) {
                    CircularProgressIndicator(modifier = Modifier.size(24.dp))
                }
            }
        }
    }
}

@Composable
private fun InvoiceCard(
    invoice: Invoice,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickableWithHaptic { onClick() },
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            // Header: Invoice number and status
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = stringResource(R.string.invoice_number, invoice.invoiceNumber ?: ""),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                InvoiceStatusBadge(status = invoice.invoiceStatusEnum)
            }

            Spacer(modifier = Modifier.height(12.dp))

            // Period
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = stringResource(R.string.period),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Text(
                    text = invoice.period,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }

            Spacer(modifier = Modifier.height(4.dp))

            // Issue date
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = stringResource(R.string.issue_date),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Text(
                    text = DateTimeUtils.formatDate(invoice.issueDate),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }

            Spacer(modifier = Modifier.height(4.dp))

            // Due date
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                Text(
                    text = stringResource(R.string.due_date),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Text(
                    text = DateTimeUtils.formatDate(invoice.dueDate),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }

            HorizontalDivider(
                modifier = Modifier.padding(vertical = 12.dp),
                color = MaterialTheme.colorScheme.outlineVariant
            )

            // Total amount
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = stringResource(R.string.total),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = formatCurrency(invoice.totalAmount ?: 0.0, invoice.currency),
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary
                )
            }
        }
    }
}

@Composable
private fun EmptyInvoicesView() {
    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = stringResource(R.string.no_invoices),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

private fun formatCurrency(amount: Double, currency: String = "EUR"): String {
    return try {
        val format = NumberFormat.getCurrencyInstance(Locale.getDefault())
        format.currency = Currency.getInstance(currency)
        format.format(amount)
    } catch (e: Exception) {
        "$currency ${String.format("%.2f", amount)}"
    }
}
