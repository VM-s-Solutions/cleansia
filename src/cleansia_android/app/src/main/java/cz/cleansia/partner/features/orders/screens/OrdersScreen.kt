package cz.cleansia.partner.features.orders.screens

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import cz.cleansia.partner.ui.components.clickableWithHaptic
import cz.cleansia.partner.ui.components.clickableWithHapticNoRipple
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccessTime
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
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
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import kotlinx.coroutines.launch
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.compositeOver
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.orders.Order
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.domain.models.orders.PaymentStatus
import cz.cleansia.partner.features.orders.components.OrdersFilterContent
import cz.cleansia.partner.features.orders.components.ViewModeToggle
import cz.cleansia.partner.features.orders.components.WeekStripView
import cz.cleansia.partner.features.orders.viewmodels.OrderSortOption
import cz.cleansia.partner.features.orders.viewmodels.OrderTab
import cz.cleansia.partner.features.orders.viewmodels.OrdersViewMode
import cz.cleansia.partner.features.orders.viewmodels.OrdersViewModel
import cz.cleansia.partner.ui.components.CardWithLoadingOverlay
import cz.cleansia.partner.ui.components.CleansiaSnackbarHost
import cz.cleansia.partner.features.orders.components.UrgencyLevel
import cz.cleansia.partner.features.orders.components.UrgencyChip
import cz.cleansia.partner.features.orders.components.rememberOrderUrgency
import cz.cleansia.partner.ui.components.ActiveFilterChipsBar
import cz.cleansia.partner.ui.components.FilterButton
import cz.cleansia.partner.ui.components.FilterChip
import cz.cleansia.partner.ui.components.FilterBottomSheet
import cz.cleansia.partner.ui.components.HelpCard
import cz.cleansia.partner.ui.components.HelpStep
import cz.cleansia.partner.ui.components.LoadingIndicator
import cz.cleansia.partner.ui.components.OfflineBanner
import cz.cleansia.partner.features.orders.components.OrdersListSkeleton
import cz.cleansia.partner.ui.components.OrderStatusBadge
import cz.cleansia.partner.ui.components.PaymentStatusBadge
import cz.cleansia.partner.ui.components.SortButton
import cz.cleansia.partner.core.utils.DateTimeUtils
import java.text.NumberFormat
import java.util.Currency
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrdersScreen(
    onNavigateToOrderDetails: (String) -> Unit,
    onScrolled: (Boolean) -> Unit = {},
    initialTab: OrderTab? = null,
    initialStatusFilter: OrderStatus? = null,
    listState: LazyListState = rememberLazyListState(),
    viewModel: OrdersViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val showHelpCard by viewModel.showHelpCard.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    // Apply initial tab/filter when navigated from dashboard
    LaunchedEffect(initialTab) {
        initialTab?.let { tab ->
            viewModel.selectTab(tab, initialStatusFilter)
        }
    }

    // Auto-refresh when returning to this screen (e.g., after completing an order).
    // In Compose Navigation, when navigating to a detail screen, the parent's
    // NavBackStackEntry lifecycle goes RESUMED → STARTED. When returning via
    // popBackStack(), it goes back to RESUMED. We track this transition.
    val lifecycleOwner = LocalLifecycleOwner.current
    var hasNavigatedAway by remember { mutableStateOf(false) }
    LaunchedEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            when (event) {
                Lifecycle.Event.ON_PAUSE -> hasNavigatedAway = true
                Lifecycle.Event.ON_RESUME -> {
                    if (hasNavigatedAway) {
                        hasNavigatedAway = false
                        viewModel.refresh()
                    }
                }
                else -> {}
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
    }

    // Show error in snackbar
    LaunchedEffect(uiState.error) {
        uiState.error?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    // Build active filter chips
    val activeFilterChips = buildActiveFilterChips(uiState.filterState)

    // Help card steps
    val helpSteps = listOf(
        HelpStep(1, stringResource(R.string.orders_help_step1_title), stringResource(R.string.orders_help_step1_desc)),
        HelpStep(2, stringResource(R.string.orders_help_step2_title), stringResource(R.string.orders_help_step2_desc)),
        HelpStep(3, stringResource(R.string.orders_help_step3_title), stringResource(R.string.orders_help_step3_desc)),
        HelpStep(4, stringResource(R.string.orders_help_step4_title), stringResource(R.string.orders_help_step4_desc))
    )

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
                    ViewModeToggle(
                        currentMode = uiState.viewMode,
                        onModeChange = { viewModel.setViewMode(it) }
                    )
                }

                // Tabs - Glass styled
                GlassTabRow(
                    selectedTab = uiState.selectedTab,
                    onTabSelected = { viewModel.onTabSelected(it) }
                )

                // Active filter chips bar
                if (activeFilterChips.isNotEmpty()) {
                    ActiveFilterChipsBar(
                        chips = activeFilterChips,
                        onRemoveChip = { viewModel.removeFilter(it) },
                        onClearAll = { viewModel.resetFilters() }
                    )
                }

                // Help card
                HelpCard(
                    title = stringResource(R.string.orders_help_title),
                    steps = helpSteps,
                    isVisible = showHelpCard,
                    onDismiss = { viewModel.dismissHelpCard() },
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)
                )

                // Offline banner
                AnimatedVisibility(
                    visible = uiState.isOffline && uiState.isShowingCachedData,
                    enter = expandVertically() + fadeIn(),
                    exit = shrinkVertically() + fadeOut()
                ) {
                    OfflineBanner(lastCachedAt = uiState.lastCachedAt)
                }

                // Week strip (when in week mode)
                if (uiState.viewMode == OrdersViewMode.WEEK) {
                    WeekStripView(
                        weekStartDate = uiState.weekStartDate,
                        selectedDate = uiState.selectedWeekDate,
                        ordersPerDay = uiState.ordersCountByDay,
                        onDateSelected = { viewModel.selectWeekDate(it) },
                        onNavigateWeek = { viewModel.navigateWeek(it) },
                        modifier = Modifier.padding(vertical = 8.dp)
                    )
                }

                // Syncing indicator
                AnimatedVisibility(visible = uiState.isSyncing) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 4.dp),
                        horizontalArrangement = Arrangement.Center,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(12.dp),
                            strokeWidth = 2.dp,
                            color = MaterialTheme.colorScheme.primary
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = stringResource(R.string.syncing),
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }

                // Content
                when {
                    uiState.isLoading -> {
                        OrdersListSkeleton(modifier = Modifier.fillMaxSize())
                    }
                    else -> {
                        val isWeekMode = uiState.viewMode == OrdersViewMode.WEEK
                        val orders = if (isWeekMode) {
                            uiState.filteredOrdersForSelectedDay
                        } else {
                            when (uiState.selectedTab) {
                                OrderTab.AVAILABLE -> uiState.availableOrders
                                OrderTab.MY_ORDERS -> uiState.myOrders
                            }
                        }

                        val hasMore = if (isWeekMode) {
                            false
                        } else {
                            when (uiState.selectedTab) {
                                OrderTab.AVAILABLE -> uiState.hasMoreAvailable
                                OrderTab.MY_ORDERS -> uiState.hasMoreMyOrders
                            }
                        }

                        PullToRefreshBox(
                            isRefreshing = uiState.isRefreshing && !uiState.isOffline,
                            onRefresh = { if (!uiState.isOffline) viewModel.refresh() },
                            modifier = Modifier.fillMaxSize()
                        ) {
                            if (orders.isEmpty()) {
                                val emptyMessage = if (isWeekMode) {
                                    stringResource(R.string.no_orders_for_day)
                                } else {
                                    when (uiState.selectedTab) {
                                        OrderTab.AVAILABLE -> stringResource(R.string.no_orders_available)
                                        OrderTab.MY_ORDERS -> stringResource(R.string.no_active_orders)
                                    }
                                }
                                EmptyOrdersView(message = emptyMessage)
                            } else {
                                OrdersList(
                                    orders = orders,
                                    hasMore = hasMore,
                                    isLoadingMore = uiState.isLoadingMore,
                                    scrollToTop = uiState.scrollToTop,
                                    selectedTab = uiState.selectedTab,
                                    hasOrderInProgress = uiState.hasOrderInProgress,
                                    listState = listState,
                                    onScrollToTopConsumed = { viewModel.consumeScrollToTop() },
                                    onOrderClick = onNavigateToOrderDetails,
                                    onTakeOrder = { viewModel.takeOrder(it) },
                                    onStartOrder = { viewModel.startOrder(it) },
                                    onLoadMore = { viewModel.loadMore() },
                                    onScrolled = onScrolled
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
            OrdersFilterContent(
                filterState = uiState.pendingFilterState,
                onSearchTermChange = { viewModel.updateSearchTerm(it) },
                onOrderStatusToggle = { viewModel.toggleOrderStatus(it) },
                onPaymentStatusToggle = { viewModel.togglePaymentStatus(it) },
                onStartDateChange = { viewModel.setStartDate(it) },
                onEndDateChange = { viewModel.setEndDate(it) }
            )
        }

        CleansiaSnackbarHost(hostState = snackbarHostState)
    }
}

/**
 * Build active filter chips from filter state
 */
@Composable
private fun buildActiveFilterChips(filterState: cz.cleansia.partner.features.orders.viewmodels.OrderFilterState): List<FilterChip> {
    val chips = mutableListOf<FilterChip>()

    // Pre-resolve all status display names
    val orderStatusNameMap = mapOf(
        OrderStatus.PENDING to stringResource(R.string.status_pending),
        OrderStatus.CONFIRMED to stringResource(R.string.status_confirmed),
        OrderStatus.IN_PROGRESS to stringResource(R.string.status_in_progress),
        OrderStatus.COMPLETED to stringResource(R.string.status_completed),
        OrderStatus.CANCELLED to stringResource(R.string.status_cancelled)
    )
    val paymentStatusNameMap = mapOf(
        PaymentStatus.PENDING to stringResource(R.string.payment_pending),
        PaymentStatus.PAID to stringResource(R.string.payment_paid),
        PaymentStatus.FAILED to stringResource(R.string.payment_failed),
        PaymentStatus.REFUNDED to stringResource(R.string.payment_refunded)
    )

    if (filterState.searchTerm.isNotBlank()) {
        chips.add(FilterChip("search", stringResource(R.string.search), filterState.searchTerm))
    }

    if (filterState.orderStatuses.isNotEmpty()) {
        val statusNames = filterState.orderStatuses.joinToString(", ") { orderStatusNameMap[it] ?: "" }
        chips.add(FilterChip("orderStatus", stringResource(R.string.order_status_filter), statusNames))
    }

    if (filterState.paymentStatuses.isNotEmpty()) {
        val statusNames = filterState.paymentStatuses.joinToString(", ") { paymentStatusNameMap[it] ?: "" }
        chips.add(FilterChip("paymentStatus", stringResource(R.string.payment_status_filter), statusNames))
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
private fun OrdersList(
    orders: List<Order>,
    hasMore: Boolean,
    isLoadingMore: Boolean,
    scrollToTop: Boolean = false,
    selectedTab: OrderTab = OrderTab.AVAILABLE,
    hasOrderInProgress: Boolean = false,
    listState: LazyListState = rememberLazyListState(),
    onScrollToTopConsumed: () -> Unit = {},
    onOrderClick: (String) -> Unit,
    onTakeOrder: suspend (String) -> Boolean = { true },
    onStartOrder: suspend (String) -> Boolean = { true },
    onLoadMore: () -> Unit,
    onScrolled: (Boolean) -> Unit = {}
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
            lastVisibleItem != null && lastVisibleItem.index >= orders.size - 3
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
        items(orders, key = { it.id }) { order ->
            OrderCard(
                order = order,
                isEmployeeAssigned = selectedTab == OrderTab.MY_ORDERS,
                hasOrderInProgress = hasOrderInProgress,
                onClick = { onOrderClick(order.id) },
                onTakeOrder = { onTakeOrder(order.id) },
                onStartOrder = { onStartOrder(order.id) }
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

private enum class QuickAction { TAKE, START }

@Composable
private fun OrderCard(
    order: Order,
    isEmployeeAssigned: Boolean = false,
    hasOrderInProgress: Boolean = false,
    onClick: () -> Unit,
    onTakeOrder: suspend () -> Boolean = { true },
    onStartOrder: suspend () -> Boolean = { true }
) {
    val coroutineScope = rememberCoroutineScope()
    var isActionLoading by remember { mutableStateOf(false) }

    // Compute urgency for this order
    val urgency = rememberOrderUrgency(order.cleaningDateTime, order.status)

    // Determine what quick action to show based on status and assignment
    val quickAction = when {
        // Employee already assigned + PENDING = waiting for others, no action
        order.status == OrderStatus.PENDING && isEmployeeAssigned -> null
        // Not assigned + PENDING = can take the order
        order.status == OrderStatus.PENDING -> QuickAction.TAKE
        // CONFIRMED + assigned (My Orders tab only) = can start the order
        order.status == OrderStatus.CONFIRMED && isEmployeeAssigned -> QuickAction.START
        else -> null
    }

    // Start Order is disabled when another order is already in progress
    val isStartDisabled = quickAction == QuickAction.START && hasOrderInProgress

    // Whether to show urgency accent stripe
    val showAccent = urgency.accentColor != null &&
        urgency.level in listOf(UrgencyLevel.MEDIUM, UrgencyLevel.HIGH, UrgencyLevel.OVERDUE)

    // Subtle tinted container for urgent orders
    val cardContainerColor = if (showAccent) {
        urgency.accentColor!!.copy(alpha = 0.04f)
            .compositeOver(MaterialTheme.colorScheme.surface)
    } else {
        MaterialTheme.colorScheme.surface
    }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickableWithHaptic { onClick() },
        colors = CardDefaults.cardColors(
            containerColor = cardContainerColor
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        CardWithLoadingOverlay(isLoading = isActionLoading) {
            Row(modifier = Modifier.height(IntrinsicSize.Min)) {
                // Left accent stripe
                if (showAccent) {
                    Box(
                        modifier = Modifier
                            .fillMaxHeight()
                            .width(4.dp)
                            .background(urgency.accentColor!!)
                    )
                }
            Column(
                modifier = Modifier
                    .weight(1f)
                    .padding(16.dp)
            ) {
                // Header: Order number and price
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = stringResource(R.string.order_id, order.orderNumber),
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.onSurface,
                        modifier = Modifier.weight(1f)
                    )

                    Text(
                        text = formatCurrency(order.totalAmount, order.currencyCode),
                        style = MaterialTheme.typography.titleSmall,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.primary,
                        textAlign = TextAlign.End
                    )
                }

                Spacer(modifier = Modifier.height(8.dp))

                // Status badges + urgency chip
                Row(
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    OrderStatusBadge(status = order.status)
                    PaymentStatusBadge(status = order.paymentStatusEnum)
                    UrgencyChip(urgency = urgency)
                }

                // Unified full-width action button (TAKE or START)
                if (quickAction != null) {
                    Spacer(modifier = Modifier.height(10.dp))

                    val (actionLabel, actionIcon, onAction) = when (quickAction) {
                        QuickAction.TAKE -> Triple(
                            stringResource(R.string.take_order),
                            Icons.Default.Check,
                            onTakeOrder
                        )
                        QuickAction.START -> Triple(
                            stringResource(R.string.start_order),
                            Icons.Default.PlayArrow,
                            onStartOrder
                        )
                    }
                    val isButtonEnabled = !isActionLoading &&
                        !(quickAction == QuickAction.START && isStartDisabled)
                    val buttonBg = if (quickAction == QuickAction.START && isStartDisabled)
                        MaterialTheme.colorScheme.onSurface.copy(alpha = 0.12f)
                    else MaterialTheme.colorScheme.primary
                    val buttonContentColor = if (quickAction == QuickAction.START && isStartDisabled)
                        MaterialTheme.colorScheme.onSurface.copy(alpha = 0.38f)
                    else MaterialTheme.colorScheme.onPrimary

                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(10.dp))
                            .background(buttonBg)
                            .clickable(enabled = isButtonEnabled) {
                                coroutineScope.launch {
                                    isActionLoading = true
                                    onAction()
                                    isActionLoading = false
                                }
                            }
                            .padding(vertical = 10.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(6.dp)
                        ) {
                            Icon(
                                imageVector = actionIcon,
                                contentDescription = actionLabel,
                                tint = buttonContentColor,
                                modifier = Modifier.size(18.dp)
                            )
                            Text(
                                text = actionLabel,
                                style = MaterialTheme.typography.labelLarge,
                                fontWeight = FontWeight.SemiBold,
                                color = buttonContentColor
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(12.dp))

                // Address row (always just address, no inline button)
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.Top
                ) {
                    Icon(
                        imageVector = Icons.Default.LocationOn,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(16.dp)
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Text(
                        text = order.address,
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis
                    )
                }

                Spacer(modifier = Modifier.height(4.dp))

                // Scheduled date
                Row(
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(
                        imageVector = Icons.Default.AccessTime,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(16.dp)
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Text(
                        text = DateTimeUtils.formatDateTimeCompact(order.scheduledDate),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }

                // Services
                if (order.services.isNotEmpty()) {
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = order.services.joinToString(", "),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }
            }
            } // Row (accent stripe + content)
        }
    }
}

@Composable
private fun EmptyOrdersView(message: String) {
    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = message,
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

private fun formatCurrency(amount: Double, currency: String = "CZK"): String {
    return try {
        val format = NumberFormat.getCurrencyInstance(Locale.getDefault())
        format.currency = Currency.getInstance(currency)
        format.format(amount)
    } catch (e: Exception) {
        "$currency ${String.format("%.2f", amount)}"
    }
}

@Composable
private fun GlassTabRow(
    selectedTab: OrderTab,
    onTabSelected: (OrderTab) -> Unit,
    modifier: Modifier = Modifier
) {
    val tabShape = RoundedCornerShape(16.dp)

    Row(
        modifier = modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 4.dp)
            .clip(tabShape)
            .background(MaterialTheme.colorScheme.surface.copy(alpha = 0.92f))
            .border(
                width = 0.5.dp,
                color = MaterialTheme.colorScheme.outline.copy(alpha = 0.15f),
                shape = tabShape
            )
            .padding(4.dp),
        horizontalArrangement = Arrangement.SpaceEvenly
    ) {
        OrderTab.entries.forEach { tab ->
            val isSelected = selectedTab == tab
            val tabBgColor = if (isSelected)
                MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.8f)
            else
                Color.Transparent
            val tabTextColor = if (isSelected)
                MaterialTheme.colorScheme.onPrimaryContainer
            else
                MaterialTheme.colorScheme.onSurfaceVariant

            val tabLabel = when (tab) {
                OrderTab.AVAILABLE -> stringResource(R.string.available)
                OrderTab.MY_ORDERS -> stringResource(R.string.my_orders)
            }

            Box(
                modifier = Modifier
                    .weight(1f)
                    .clip(RoundedCornerShape(12.dp))
                    .background(tabBgColor)
                    .clickableWithHapticNoRipple { onTabSelected(tab) }
                    .padding(vertical = 10.dp),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = tabLabel,
                    style = MaterialTheme.typography.labelLarge,
                    fontWeight = if (isSelected) FontWeight.SemiBold else FontWeight.Normal,
                    color = tabTextColor
                )
            }
        }
    }
}

@Composable
private fun SortDropdownMenu(
    expanded: Boolean,
    currentSort: OrderSortOption,
    onDismiss: () -> Unit,
    onSortSelected: (OrderSortOption) -> Unit
) {
    DropdownMenu(
        expanded = expanded,
        onDismissRequest = onDismiss
    ) {
        OrderSortOption.entries.forEach { option ->
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
