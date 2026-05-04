package cz.cleansia.customer.features.orders

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CloudOff
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.material3.pulltorefresh.rememberPullToRefreshState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.format.formatOrderDateRange
import cz.cleansia.customer.core.format.formatOrderPrice
import cz.cleansia.customer.core.format.orderStatusColor
import cz.cleansia.customer.core.orders.OrderListItemDto
import cz.cleansia.customer.core.orders.OrderRepositoryEntryPoint
import cz.cleansia.customer.ui.components.CleansiaPrimaryButton
import cz.cleansia.customer.ui.theme.Poppins
import dagger.hilt.android.EntryPointAccessors
import kotlinx.coroutines.launch

/**
 * Orders tab — lists the signed-in user's cleaning orders with filter chips,
 * infinite scroll and pull-to-refresh. Data comes from the singleton
 * [cz.cleansia.customer.core.orders.OrderRepository], which MainShell
 * prefetches on first composition so the list is ready when the user taps
 * the tab. Wire DTOs are consumed directly — any shared formatting lives in
 * [cz.cleansia.customer.core.format.OrderFormatters].
 */

private enum class OrderFilter { All, Upcoming, Completed, Cancelled }

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrdersTab(
    modifier: Modifier = Modifier,
    onOrderClick: (orderId: String) -> Unit = {},
    onBookCleaning: () -> Unit = {},
) {
    val context = LocalContext.current
    val orderRepo = remember {
        EntryPointAccessors
            .fromApplication(context, OrderRepositoryEntryPoint::class.java)
            .orderRepository()
    }
    val scope = rememberCoroutineScope()

    val orders by orderRepo.orders.collectAsState()
    val loading by orderRepo.loading.collectAsState()
    val loadingMore by orderRepo.loadingMore.collectAsState()
    val loaded by orderRepo.loaded.collectAsState()
    val totalRecords by orderRepo.totalRecords.collectAsState()

    var activeFilter by remember { mutableStateOf(OrderFilter.All) }

    // Status value ranges per filter. Matches backend OrderStatus enum:
    // New=0, Pending=1, Confirmed=2, InProgress=3 (all "upcoming"),
    // Completed=4, Cancelled=5.
    val filtered = remember(orders, activeFilter) {
        when (activeFilter) {
            OrderFilter.All -> orders
            OrderFilter.Upcoming -> orders.filter { (it.orderStatus?.value ?: -1) in 0..3 }
            OrderFilter.Completed -> orders.filter { it.orderStatus?.value == 4 }
            OrderFilter.Cancelled -> orders.filter { it.orderStatus?.value == 5 }
        }
    }

    val pullState = rememberPullToRefreshState()
    val refresh: () -> Unit = { scope.launch { orderRepo.refresh() } }

    // Safety-net auto-refresh on tab entry. The MainShell prefetch only fires
    // once per shell composition; if a booking is created between the initial
    // prefetch and the first time the user opens this tab, the cache would be
    // stale. Trigger a background refresh on every tab entry — gated on
    // `loading` to avoid stacking parallel calls. Pull-to-refresh stays
    // available for explicit manual refreshes.
    LaunchedEffect(Unit) {
        if (!orderRepo.loading.value) orderRepo.refresh()
    }

    Column(
        modifier = modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        Text(
            text = stringResource(R.string.orders_title),
            style = MaterialTheme.typography.headlineMedium.copy(
                fontFamily = Poppins,
                fontWeight = FontWeight.Bold,
            ),
            color = MaterialTheme.colorScheme.onBackground,
            modifier = Modifier.padding(horizontal = 20.dp, vertical = 16.dp),
        )

        // Lift PullToRefreshBox above the state branches so the gesture is
        // available on every state — empty / error / loaded — not just when
        // the list has rows. The empty/error branches render in a vertically
        // scrollable container so the pull gesture has something to attach to.
        PullToRefreshBox(
            isRefreshing = loading,
            onRefresh = refresh,
            state = pullState,
            modifier = Modifier.fillMaxSize(),
        ) {
            when {
                loading && !loaded -> OrdersLoading()
                !loaded && orders.isEmpty() -> ScrollableStateContainer {
                    OrdersError(onRetry = refresh)
                }
                orders.isEmpty() -> ScrollableStateContainer {
                    OrdersEmpty(onBookCleaning = onBookCleaning)
                }
                else -> OrdersContent(
                    allOrders = orders,
                    filtered = filtered,
                    activeFilter = activeFilter,
                    onFilterChange = { activeFilter = it },
                    loadingMore = loadingMore,
                    hasMore = orders.size < totalRecords,
                    onLoadMore = { scope.launch { orderRepo.loadNextPage() } },
                    onOrderClick = onOrderClick,
                )
            }
        }
    }
}

/**
 * Wraps an otherwise-static state composable (empty/error mascot) in a
 * verticalScroll Column so PullToRefreshBox has a scrollable child to attach
 * the drag-down gesture to. Without this, the pull gesture is swallowed and
 * never triggers refresh on the empty state.
 *
 * BoxWithConstraints is the trick that lets the inner content be vertically
 * centered: a verticalScroll Column normally collapses to its content height,
 * which would leave the centered child stuck at the top. By forcing the inner
 * Box's `minHeight` to the parent's available height, `Alignment.Center`
 * actually centers within the full screen height.
 */
@Composable
private fun ScrollableStateContainer(content: @Composable () -> Unit) {
    androidx.compose.foundation.layout.BoxWithConstraints(
        modifier = Modifier.fillMaxSize(),
    ) {
        val minHeight = maxHeight
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState()),
        ) {
            // Force the inner box to the FULL viewport height (not just min)
            // so children using `fillMaxSize` + `contentAlignment = Center`
            // actually center within the visible area. With `heightIn(min)` the
            // box sizes to its content, leaving the centered child stuck at
            // wherever the column laid it out.
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(minHeight),
                contentAlignment = Alignment.Center,
            ) {
                content()
            }
        }
    }
}

/* ── Content ── */

@Composable
private fun OrdersContent(
    allOrders: List<OrderListItemDto>,
    filtered: List<OrderListItemDto>,
    activeFilter: OrderFilter,
    onFilterChange: (OrderFilter) -> Unit,
    loadingMore: Boolean,
    hasMore: Boolean,
    onLoadMore: () -> Unit,
    onOrderClick: (String) -> Unit,
) {
    val listState = rememberLazyListState()
    val shouldLoadMore by remember(hasMore, loadingMore) {
        derivedStateOf {
            val last = listState.layoutInfo.visibleItemsInfo.lastOrNull()?.index ?: 0
            val total = listState.layoutInfo.totalItemsCount
            hasMore && !loadingMore && total > 0 && last >= total - 3
        }
    }
    LaunchedEffect(shouldLoadMore) {
        if (shouldLoadMore) onLoadMore()
    }

    // Precompute per-filter counts so chips can show "Upcoming 3" style labels.
    val counts = remember(allOrders) {
        val up = allOrders.count { (it.orderStatus?.value ?: -1) in 0..3 }
        val done = allOrders.count { it.orderStatus?.value == 4 }
        val cancel = allOrders.count { it.orderStatus?.value == 5 }
        mapOf(
            OrderFilter.All to allOrders.size,
            OrderFilter.Upcoming to up,
            OrderFilter.Completed to done,
            OrderFilter.Cancelled to cancel,
        )
    }

    Column(Modifier.fillMaxSize()) {
        FilterChipsRow(
            active = activeFilter,
            counts = counts,
            onChange = onFilterChange,
        )
        Spacer(Modifier.height(8.dp))

        // Pull-to-refresh wraps this content + the empty/error states from the
        // parent — see OrdersTab. The list itself is just a LazyColumn.
        LazyColumn(
            state = listState,
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(horizontal = 20.dp, vertical = 4.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            if (filtered.isEmpty()) {
                item { FilteredEmptyNote(activeFilter) }
            } else {
                items(filtered, key = { it.id ?: it.hashCode().toString() }) { order ->
                    OrderCard(order = order, onClick = {
                        order.id?.let(onOrderClick)
                    })
                }
            }

            if (loadingMore) {
                item { LoadingMoreRow() }
            }

            item { Spacer(Modifier.height(32.dp)) }
        }
    }
}

@Composable
private fun FilterChipsRow(
    active: OrderFilter,
    counts: Map<OrderFilter, Int>,
    onChange: (OrderFilter) -> Unit,
) {
    val items = listOf(
        Triple(OrderFilter.All, R.string.orders_filter_all, counts[OrderFilter.All] ?: 0),
        Triple(OrderFilter.Upcoming, R.string.orders_filter_upcoming, counts[OrderFilter.Upcoming] ?: 0),
        Triple(OrderFilter.Completed, R.string.orders_filter_completed, counts[OrderFilter.Completed] ?: 0),
        Triple(OrderFilter.Cancelled, R.string.orders_filter_cancelled, counts[OrderFilter.Cancelled] ?: 0),
    )
    LazyRow(
        contentPadding = PaddingValues(horizontal = 20.dp),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        items(items) { (filter, labelRes, count) ->
            OrderFilterChip(
                label = stringResource(labelRes),
                count = count,
                selected = filter == active,
                onClick = { onChange(filter) },
            )
        }
    }
}

@Composable
private fun OrderFilterChip(
    label: String,
    count: Int,
    selected: Boolean,
    onClick: () -> Unit,
) {
    val tint = MaterialTheme.colorScheme.primary
    val labelText = if (count > 0) {
        stringResource(R.string.orders_filter_count, label, count)
    } else {
        label
    }
    Row(
        modifier = Modifier
            .clip(RoundedCornerShape(999.dp))
            .background(if (selected) tint else MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                if (selected) tint else MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(999.dp),
            )
            .clickable(onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            labelText,
            style = MaterialTheme.typography.labelLarge.copy(
                fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Normal,
            ),
            color = if (selected) Color.White else MaterialTheme.colorScheme.onSurface,
        )
    }
}

/* ── Order card ── */

@Composable
private fun OrderCard(
    order: OrderListItemDto,
    onClick: () -> Unit,
) {
    val statusColor = orderStatusColor(order.orderStatus?.value)
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(IntrinsicSize.Min)
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(16.dp),
            )
            .clickable(onClick = onClick),
    ) {
        // Status bar — vertical accent strip, same color family as the pill.
        // The outer Row uses IntrinsicSize.Min so fillMaxHeight() below stretches
        // the strip to the height of the content Column next to it.
        Box(
            modifier = Modifier
                .width(4.dp)
                .fillMaxHeight()
                .background(statusColor),
        )
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
        ) {
            // Order number + status pill
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                Text(
                    text = order.displayOrderNumber?.let { "#$it" } ?: "—",
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                StatusPill(
                    label = order.orderStatus?.name ?: "—",
                    color = statusColor,
                )
            }
            Spacer(Modifier.height(6.dp))

            // Date + time range
            Text(
                text = formatOrderDateRange(
                    iso = order.cleaningDateTime,
                    estimatedMinutes = order.estimatedTime,
                ),
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onBackground,
            )
            Spacer(Modifier.height(2.dp))

            // Address
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    Icons.Outlined.LocationOn,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(14.dp),
                )
                Spacer(Modifier.width(6.dp))
                Text(
                    text = order.customerAddress ?: "—",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
            Spacer(Modifier.height(10.dp))

            // Services summary + price
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                Text(
                    text = servicesSummary(order),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis,
                    modifier = Modifier.weight(1f),
                )
                Spacer(Modifier.width(8.dp))
                Text(
                    text = formatOrderPrice(order.totalPrice, order.currency?.code),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onBackground,
                )
            }
        }
    }
}

@Composable
private fun StatusPill(label: String, color: Color) {
    Row(
        modifier = Modifier
            .clip(RoundedCornerShape(999.dp))
            .background(color.copy(alpha = 0.14f))
            .padding(horizontal = 10.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = color,
        )
    }
}

/**
 * Single-line summary: up to 2 package names first (memorable), then services,
 * with "+ N more" suffix if there are additional items.
 */
@Composable
private fun servicesSummary(order: OrderListItemDto): String {
    val packageNames = order.selectedPackages.orEmpty().mapNotNull { it.name?.takeIf { n -> n.isNotBlank() } }
    val serviceNames = order.selectedServices.orEmpty().mapNotNull { it.name?.takeIf { n -> n.isNotBlank() } }
    val combined = packageNames + serviceNames
    if (combined.isEmpty()) return "—"
    val shown = combined.take(2)
    val remaining = combined.size - shown.size
    val base = shown.joinToString(", ")
    return if (remaining > 0) {
        "$base ${stringResource(R.string.orders_services_more, remaining)}"
    } else {
        base
    }
}

/* ── States ── */

@Composable
private fun OrdersLoading() {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 20.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        repeat(3) { SkeletonCard() }
    }
}

@Composable
private fun SkeletonCard() {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                1.dp,
                MaterialTheme.colorScheme.outlineVariant,
                RoundedCornerShape(16.dp),
            )
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        SkeletonBar(widthFraction = 0.35f, height = 12.dp)
        SkeletonBar(widthFraction = 0.7f, height = 18.dp)
        SkeletonBar(widthFraction = 0.55f, height = 12.dp)
        SkeletonBar(widthFraction = 0.9f, height = 14.dp)
    }
}

@Composable
private fun SkeletonBar(widthFraction: Float, height: androidx.compose.ui.unit.Dp) {
    Box(
        modifier = Modifier
            .fillMaxWidth(widthFraction)
            .height(height)
            .clip(RoundedCornerShape(6.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant),
    )
}

@Composable
private fun OrdersError(onRetry: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(40.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            Icons.Outlined.CloudOff,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(40.dp),
        )
        Spacer(Modifier.height(12.dp))
        Text(
            text = stringResource(R.string.orders_error_title),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(12.dp))
        Text(
            text = stringResource(R.string.orders_error_retry),
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier
                .clip(RoundedCornerShape(999.dp))
                .clickable(onClick = onRetry)
                .padding(horizontal = 16.dp, vertical = 8.dp),
        )
    }
}

@Composable
private fun OrdersEmpty(onBookCleaning: () -> Unit) {
    // Outer Box vertically centers the content within the available tab area
    // (which already has the bottom-bar padding stripped by the Scaffold). The
    // inner Column owns the actual stacking; horizontal centering happens via
    // the Box alignment so the Column doesn't need to fillMaxSize.
    Box(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 20.dp),
        contentAlignment = Alignment.Center,
    ) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Image(
                painterResource(R.drawable.mascot_idea),
                contentDescription = null,
                modifier = Modifier.size(160.dp),
            )
            Spacer(Modifier.height(24.dp))
            Text(
                text = stringResource(R.string.orders_empty_title),
                style = MaterialTheme.typography.headlineSmall.copy(fontFamily = Poppins, fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onBackground,
            )
            Spacer(Modifier.height(8.dp))
            Text(
                text = stringResource(R.string.orders_empty_subtitle),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(24.dp))
            CleansiaPrimaryButton(
                text = stringResource(R.string.orders_empty_cta),
                onClick = onBookCleaning,
            )
        }
    }
}

@Composable
private fun FilteredEmptyNote(filter: OrderFilter) {
    val labelRes = when (filter) {
        OrderFilter.All -> R.string.orders_filter_all
        OrderFilter.Upcoming -> R.string.orders_filter_upcoming
        OrderFilter.Completed -> R.string.orders_filter_completed
        OrderFilter.Cancelled -> R.string.orders_filter_cancelled
    }
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 32.dp),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            text = stringResource(labelRes) + " · 0",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun LoadingMoreRow() {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 12.dp),
        contentAlignment = Alignment.Center,
    ) {
        CircularProgressIndicator(
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(22.dp),
            strokeWidth = 2.dp,
        )
    }
}
