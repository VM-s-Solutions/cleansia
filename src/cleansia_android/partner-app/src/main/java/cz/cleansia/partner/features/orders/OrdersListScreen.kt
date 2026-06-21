package cz.cleansia.partner.features.orders

import android.Manifest
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowForward
import androidx.compose.material.icons.outlined.ArrowDropDown
import androidx.compose.material.icons.outlined.LocalFireDepartment
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material.icons.outlined.PlayCircle
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.material3.pulltorefresh.rememberPullToRefreshState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.MascotEmptyState
import cz.cleansia.core.ui.components.SudsRefreshIndicator
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.features.main.MainBottomNavInset
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import java.util.Locale
import kotlin.math.roundToInt

/**
 * Wolt/Bolt-style cleaner job board.
 *
 *  - Top: title + segmented control (Available / Active / History).
 *  - Sticky in-progress banner above everything when a job is in flight,
 *    so the cleaner can never lose track of what they're doing.
 *  - Available pane: stack of opportunity cards with map preview, distance
 *    hero metric, pay, meta, and a single primary Take button. No reject —
 *    skipping is implicit (just scroll past).
 *  - Active pane: list of confirmed/on-the-way orders with inline
 *    status-advance buttons.
 *  - History pane: period filter + earnings summary + day-grouped rows.
 *
 * "Tab" terminology survives in the VM (`OrdersTab`) for now; only the
 * surface (segmented control vs TabRow) changed.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrdersListScreen(
    onOrderClick: (String) -> Unit,
    viewModel: OrdersListViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val statusBarTop = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()

    val permissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission(),
    ) { granted -> viewModel.onLocationPermissionResult(granted) }

    LaunchedEffect(uiState.tab) {
        if (uiState.tab == OrdersTab.Available) {
            if (!uiState.hasLocationPermission) {
                permissionLauncher.launch(Manifest.permission.ACCESS_FINE_LOCATION)
            } else {
                // Re-resolve every time the cleaner enters the Available tab.
                // VM.init() only runs once; without this the first fix attempt
                // (often null on a cold emulator with no mock location) sticks
                // forever. Re-entering the tab now retries.
                viewModel.refreshLocation()
            }
        }
    }

    // Returning from OrderDetailScreen (after take / on-the-way / start
    // / complete) used to fire a visible pull indicator on every resume.
    // Route through the cached/stale-checking path instead: a warm cache
    // (within the Staleness window, ~30s default) means no network call
    // at all; a stale cache means a silent background fetch with no
    // chunky pull indicator. The mutation paths in the VM call
    // repo.invalidatePanesFor() first, so a real change is always visible.
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) {
        viewModel.onResume()
    }

    val inProgress = remember(uiState.orders) {
        // Sticky banner only shows when there's actually an in-progress job
        // *currently in state*. We don't peek across tabs because the VM only
        // holds the current tab's orders — that's fine: the banner is most
        // relevant on the Active tab where the user lives during a job.
        uiState.orders.firstOrNull { it.orderStatus.toOrderStatus() == OrderStatus._4 }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        Spacer(Modifier.height(statusBarTop))
        Text(
            text = stringResource(R.string.orders),
            style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
            color = MaterialTheme.colorScheme.onBackground,
            modifier = Modifier.padding(horizontal = Spacing.M, vertical = Spacing.M),
        )

        SegmentedTabs(
            current = uiState.tab,
            onSelect = viewModel::selectTab,
            modifier = Modifier.padding(horizontal = Spacing.M),
        )

        Spacer(Modifier.height(Spacing.M))

        if (inProgress != null) {
            InProgressStickyBanner(
                order = inProgress,
                onClick = { inProgress.id?.let(onOrderClick) },
                modifier = Modifier.padding(horizontal = Spacing.M),
            )
            Spacer(Modifier.height(Spacing.S))
        }

        // Pull-to-refresh wraps all three panes so the gesture is
        // available regardless of which tab is active. The initial
        // load still shows the centered CircularProgressIndicator so
        // pull-down isn't competing with the first paint. Subsequent
        // USER refreshes drive the built-in PTR spinner at the top of
        // the pane — background refreshes (auto-resume, post-mutation,
        // sort/period change) do NOT, by design.
        val pullState = rememberPullToRefreshState()
        // True only on the very first load (before any refresh has finished).
        // After that, even when the list is empty, we want to show the empty
        // mascot inside the PullToRefreshBox so pull-to-refresh keeps working
        // — matches InvoicesListScreen.
        val isInitialLoad = uiState.isInitialLoad && !uiState.hasLoadedOnce
        PullToRefreshBox(
            // INVARIANT: PullToRefreshBox subscribes to isUserRefreshing
            // ONLY. Never isBackgroundRefreshing, never a generic isLoading.
            // The chunky pull indicator must only fire from a user pull.
            isRefreshing = uiState.isUserRefreshing,
            onRefresh = { viewModel.onRefresh() },
            state = pullState,
            modifier = Modifier.fillMaxSize(),
            indicator = {
                SudsRefreshIndicator(
                    state = pullState,
                    isRefreshing = uiState.isUserRefreshing,
                    modifier = Modifier
                        .align(Alignment.TopCenter)
                        .padding(top = statusBarTop + 8.dp),
                )
            },
        ) {
            when {
                isInitialLoad -> {
                    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator()
                    }
                }
                else -> when (uiState.tab) {
                    OrdersTab.Available -> AvailablePane(
                        uiState = uiState,
                        onOrderClick = onOrderClick,
                        onSearchChange = viewModel::setSearchQuery,
                        onSortSelected = viewModel::setAvailableSort,
                        onTakeOrder = viewModel::takeOrderInline,
                    )
                    OrdersTab.MyActive -> ActivePane(
                        uiState = uiState,
                        onOrderClick = onOrderClick,
                        onNotifyOnTheWay = viewModel::notifyOnTheWayInline,
                        onStart = viewModel::startOrderInline,
                        onComplete = viewModel::completeOrderInline,
                    )
                    OrdersTab.MyCompleted -> HistoryPane(
                        uiState = uiState,
                        onOrderClick = onOrderClick,
                        onPeriodSelected = viewModel::setCompletedPeriod,
                    )
                }
            }
        }
    }
}

// ─── Segmented control + sticky banner ──────────────────────────────

@Composable
private fun SegmentedTabs(
    current: OrdersTab,
    onSelect: (OrdersTab) -> Unit,
    modifier: Modifier = Modifier,
) {
    Surface(
        modifier = modifier
            .fillMaxWidth()
            .height(44.dp),
        shape = RoundedCornerShape(50),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
    ) {
        Row(
            modifier = Modifier.fillMaxSize().padding(4.dp),
            horizontalArrangement = Arrangement.spacedBy(4.dp),
        ) {
            OrdersTab.values().forEach { tab ->
                SegmentChip(
                    label = stringResource(segmentLabelFor(tab)),
                    selected = tab == current,
                    onClick = { onSelect(tab) },
                    modifier = Modifier.weight(1f),
                )
            }
        }
    }
}

private fun segmentLabelFor(tab: OrdersTab): Int = when (tab) {
    OrdersTab.Available -> R.string.available
    OrdersTab.MyActive -> R.string.active
    OrdersTab.MyCompleted -> R.string.history
}

@Composable
private fun SegmentChip(
    label: String,
    selected: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Surface(
        onClick = onClick,
        modifier = modifier.fillMaxSize(),
        shape = RoundedCornerShape(50),
        color = if (selected) MaterialTheme.colorScheme.surface else Color.Transparent,
        shadowElevation = if (selected) 2.dp else 0.dp,
    ) {
        Box(contentAlignment = Alignment.Center) {
            Text(
                text = label,
                style = MaterialTheme.typography.labelLarge,
                color = if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant,
                fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Medium,
            )
        }
    }
}

@Composable
private fun InProgressStickyBanner(
    order: OrderListItem,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Surface(
        onClick = onClick,
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        color = MaterialTheme.colorScheme.primary,
    ) {
        Row(
            modifier = Modifier.padding(horizontal = Spacing.M, vertical = Spacing.S),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(Spacing.S),
        ) {
            Icon(
                imageVector = Icons.Outlined.PlayCircle,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onPrimary,
            )
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = stringResource(R.string.in_progress_now),
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.85f),
                    fontWeight = FontWeight.SemiBold,
                )
                Text(
                    text = order.customerName?.takeIf { it.isNotBlank() }
                        ?: order.customerAddress?.takeIf { it.isNotBlank() }
                        ?: "#${order.displayOrderNumber.orEmpty()}",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onPrimary,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    fontWeight = FontWeight.Medium,
                )
            }
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.ArrowForward,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onPrimary,
            )
        }
    }
}

// ─── Available pane ─────────────────────────────────────────────────

@Composable
private fun AvailablePane(
    uiState: OrdersListUiState,
    onOrderClick: (String) -> Unit,
    onSearchChange: (String) -> Unit,
    onSortSelected: (AvailableSort) -> Unit,
    onTakeOrder: (String) -> Unit,
) {
    val filtered = remember(uiState.orders, uiState.searchQuery) {
        uiState.orders.filter { it.matchesSearch(uiState.searchQuery) }
    }
    val totalEarnings = remember(filtered) {
        filtered.sumOf { (it.estimatedCleanerPay ?: 0.0) }
    }
    // The "hot deal" badge highlights the single highest-paying offer in
    // the currently visible list. We hide it when the list is trivially
    // short (<2) so we don't decorate the only card on screen.
    val hotDealPay = remember(filtered) {
        if (filtered.size < 2) null
        else filtered.maxOfOrNull { it.estimatedCleanerPay ?: 0.0 }?.takeIf { it > 0.0 }
    }

    if (filtered.isEmpty() && uiState.searchQuery.isBlank()) {
        // Wrap the empty state in a scrollable Column so PullToRefreshBox
        // above us can still capture vertical drag gestures when the list
        // is empty. A plain Box swallows the gesture and pull-to-refresh
        // never fires. Matches the InvoicesListScreen pattern.
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState()),
        ) {
            EmptyState(tab = uiState.tab)
        }
        return
    }

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(horizontal = Spacing.M, vertical = Spacing.S),
        verticalArrangement = Arrangement.spacedBy(Spacing.S),
    ) {
        item { SearchBar(value = uiState.searchQuery, onValueChange = onSearchChange) }
        item {
            AvailableSummaryRow(
                count = filtered.size,
                totalEarnings = totalEarnings,
                currencySymbol = commonCurrencySymbol(filtered),
                currentSort = uiState.availableSort,
                onSortSelected = onSortSelected,
            )
        }
        if (filtered.isEmpty()) {
            item { EmptyInline(text = stringResource(R.string.no_matching_orders)) }
        }
        items(filtered, key = { it.id.orEmpty() }) { order ->
            val pay = order.estimatedCleanerPay ?: 0.0
            AvailableOrderRow(
                order = order,
                currentLocation = uiState.currentLocation,
                isTaking = uiState.inFlightActionOrderId != null &&
                    uiState.inFlightActionOrderId == order.id,
                isHotDeal = hotDealPay != null && pay >= hotDealPay,
                onOpenDetails = { order.id?.let(onOrderClick) },
                onTake = { order.id?.let(onTakeOrder) },
            )
        }
        item { Spacer(Modifier.height(MainBottomNavInset)) }
    }
}

@Composable
private fun AvailableSummaryRow(
    count: Int,
    totalEarnings: Double,
    currencySymbol: String?,
    currentSort: AvailableSort,
    onSortSelected: (AvailableSort) -> Unit,
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = stringResource(R.string.available_summary, count, formatMoney(totalEarnings, currencySymbol)),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        SortDropdown(currentSort = currentSort, onSortSelected = onSortSelected)
    }
}

@Composable
private fun SortDropdown(
    currentSort: AvailableSort,
    onSortSelected: (AvailableSort) -> Unit,
) {
    var expanded by remember { mutableStateOf(false) }
    Box {
        Row(
            modifier = Modifier
                .clip(RoundedCornerShape(50))
                .clickable { expanded = true }
                .padding(horizontal = 10.dp, vertical = 6.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(4.dp),
        ) {
            Text(
                text = stringResource(currentSort.labelRes),
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.primary,
                fontWeight = FontWeight.SemiBold,
            )
            Icon(
                imageVector = Icons.Outlined.ArrowDropDown,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }
        DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            AvailableSort.values().forEach { option ->
                DropdownMenuItem(
                    text = { Text(stringResource(option.labelRes)) },
                    onClick = {
                        onSortSelected(option)
                        expanded = false
                    },
                )
            }
        }
    }
}

/**
 * Available-order opportunity card. Restores the richer decision panel
 * from the original orders-page-redesign spec: full when/where header,
 * scope chips (rooms · baths · extras), distance + duration, prominent
 * pay readout, and decision badges (hot deal, starting soon). Cleaners
 * told us they want enough information *on the card* to decide whether
 * to tap through — the previous one-line compact row was too thin.
 *
 * Card stays a flat 16dp surface with a 1dp outlineVariant border to
 * match EarningsSummaryScreen's design language. Tap opens details;
 * the trailing primary CTA still fires the inline Take action so a
 * scanning cleaner can grab a job without an extra screen hop.
 */
@Composable
private fun AvailableOrderRow(
    order: OrderListItem,
    currentLocation: cz.cleansia.core.location.UserLocation?,
    isTaking: Boolean,
    isHotDeal: Boolean,
    onOpenDetails: () -> Unit,
    onTake: () -> Unit,
) {
    val pay = order.estimatedCleanerPay ?: 0.0
    val currencySymbol = order.currency?.symbol
    val distance = OrdersListViewModel.distanceKmFor(order, currentLocation)
    val isStartingSoon = remember(order.cleaningDateTime) {
        order.cleaningDateTime?.let { runCatching { Instant.parse(it) }.getOrNull() }
            ?.let { java.time.Duration.between(Instant.now(), it) }
            ?.let { it.toMinutes() in 0..120 } == true
    }
    val address = order.customerAddress?.takeIf { it.isNotBlank() }
        ?: order.customerAddressApproximate?.takeIf { it.isNotBlank() }

    // Press-scale tween — same tactile feedback the old OfferCard had,
    // tuned a touch more subtle (0.985) so the larger card doesn't feel
    // like it's collapsing when tapped.
    val interactionSource = remember { androidx.compose.foundation.interaction.MutableInteractionSource() }
    val isPressed by interactionSource.collectIsPressedAsState()
    val pressScale by androidx.compose.animation.core.animateFloatAsState(
        targetValue = if (isPressed) 0.985f else 1f,
        animationSpec = androidx.compose.animation.core.tween(durationMillis = 120),
        label = "available-row-press-scale",
    )

    Surface(
        onClick = onOpenDetails,
        interactionSource = interactionSource,
        modifier = Modifier
            .fillMaxWidth()
            .graphicsLayer(scaleX = pressScale, scaleY = pressScale),
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
    ) {
        Column(
            modifier = Modifier.padding(Spacing.M),
            verticalArrangement = Arrangement.spacedBy(Spacing.S),
        ) {
            // ── Header row: when + pay ────────────────────────────────
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.Top,
                horizontalArrangement = Arrangement.spacedBy(Spacing.S),
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    val whenText = order.cleaningDateTime?.takeIf { it.isNotBlank() }
                        ?.let { formatRelativeDateTime(it) } ?: "—"
                    Text(
                        text = whenText,
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onSurface,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                    val mins = order.estimatedTime ?: 0
                    if (mins > 0) {
                        Text(
                            text = formatDuration(mins),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
                Column(horizontalAlignment = Alignment.End) {
                    Text(
                        text = formatMoney(pay, currencySymbol),
                        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                        color = MaterialTheme.colorScheme.primary,
                    )
                    Text(
                        text = stringResource(R.string.you_earn),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }

            // ── Address row ───────────────────────────────────────────
            if (address != null || distance != null) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(6.dp),
                ) {
                    Icon(
                        imageVector = Icons.Outlined.LocationOn,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(16.dp),
                    )
                    Text(
                        text = listOfNotNull(
                            address,
                            distance?.let { stringResource(R.string.km_away, formatDistance(it)) },
                        ).joinToString(" · "),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.weight(1f),
                    )
                }
            }

            // ── Scope row: rooms / baths / extras ─────────────────────
            val rooms = order.rooms ?: 0
            val baths = order.bathrooms ?: 0
            val extras = order.extras?.count { it.value } ?: 0
            if (rooms > 0 || baths > 0 || extras > 0) {
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    if (rooms > 0) {
                        ScopeChip(text = androidx.compose.ui.res.pluralStringResource(R.plurals.scope_rooms, rooms, rooms))
                    }
                    if (baths > 0) {
                        ScopeChip(text = androidx.compose.ui.res.pluralStringResource(R.plurals.scope_baths, baths, baths))
                    }
                    if (extras > 0) {
                        ScopeChip(text = androidx.compose.ui.res.pluralStringResource(R.plurals.scope_extras, extras, extras))
                    }
                }
            }

            // ── Decision badges ───────────────────────────────────────
            if (isHotDeal || isStartingSoon) {
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    if (isHotDeal) {
                        DecisionBadge(
                            icon = Icons.Outlined.LocalFireDepartment,
                            label = stringResource(R.string.top_pay),
                            tint = MaterialTheme.colorScheme.error,
                        )
                    }
                    if (isStartingSoon) {
                        DecisionBadge(
                            icon = Icons.Outlined.Schedule,
                            label = stringResource(R.string.starts_soon),
                            tint = MaterialTheme.colorScheme.primary,
                        )
                    }
                }
            }

            // ── Take CTA ──────────────────────────────────────────────
            TakeButton(
                isTaking = isTaking,
                onTake = onTake,
                modifier = Modifier.fillMaxWidth(),
            )
        }
    }
}

/** Subtle outlined chip for scope tokens (rooms · baths · extras). */
@Composable
private fun ScopeChip(text: String) {
    Surface(
        shape = RoundedCornerShape(50),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
        )
    }
}

/** Tinted icon-plus-label badge for hot-deal / starting-soon callouts. */
@Composable
private fun DecisionBadge(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    label: String,
    tint: Color,
) {
    Surface(
        shape = RoundedCornerShape(50),
        color = tint.copy(alpha = 0.12f),
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(4.dp),
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                tint = tint,
                modifier = Modifier.size(14.dp),
            )
            Text(
                text = label,
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = tint,
            )
        }
    }
}

/**
 * Primary CTA for the Available card. Full-width pill button so the
 * cleaner has a clear target after scanning the decision panel above
 * (when / where / scope / pay / badges). While [isTaking] the label is
 * replaced by an inline spinner so the request landing is visible
 * without the button shifting size.
 */
@Composable
private fun TakeButton(
    isTaking: Boolean,
    onTake: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Surface(
        onClick = onTake,
        enabled = !isTaking,
        shape = RoundedCornerShape(50),
        color = MaterialTheme.colorScheme.primary,
        modifier = modifier,
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 10.dp)
                .height(20.dp),
            contentAlignment = Alignment.Center,
        ) {
            if (isTaking) {
                CircularProgressIndicator(
                    modifier = Modifier.size(18.dp),
                    color = MaterialTheme.colorScheme.onPrimary,
                    strokeWidth = 2.dp,
                )
            } else {
                Text(
                    text = stringResource(R.string.take_order),
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.onPrimary,
                    fontWeight = FontWeight.SemiBold,
                )
            }
        }
    }
}

// ─── Active pane ────────────────────────────────────────────────────

@Composable
private fun ActivePane(
    uiState: OrdersListUiState,
    onOrderClick: (String) -> Unit,
    onNotifyOnTheWay: (String) -> Unit,
    onStart: (String) -> Unit,
    onComplete: (String) -> Unit,
) {
    if (uiState.orders.isEmpty()) {
        // Wrap in a scrollable Column so the surrounding PullToRefreshBox
        // still receives drag gestures when the list is empty. A plain
        // Box swallows the gesture and pull-to-refresh never fires.
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState()),
        ) {
            EmptyState(tab = uiState.tab)
        }
        return
    }
    val grouped = remember(uiState.orders) {
        // In-progress order is shown by the sticky banner at the top of the
        // screen — don't repeat it inside the day groups.
        uiState.orders
            .filter { it.orderStatus.toOrderStatus() != OrderStatus._4 }
            .groupBy { ActiveDayBucket.forDate(it.cleaningLocalDate()) }
            .toSortedMap(compareBy { it.ordinal })
    }

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(horizontal = Spacing.M, vertical = Spacing.S),
        verticalArrangement = Arrangement.spacedBy(Spacing.S),
    ) {
        grouped.forEach { (bucket, ordersForBucket) ->
            item {
                Text(
                    text = stringResource(bucket.labelRes),
                    style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = Spacing.S),
                )
            }
            items(ordersForBucket, key = { it.id.orEmpty() }) { order ->
                ActiveOrderRow(
                    order = order,
                    isBusy = uiState.inFlightActionOrderId != null &&
                        uiState.inFlightActionOrderId == order.id,
                    onClick = { order.id?.let(onOrderClick) },
                    onNotifyOnTheWay = { order.id?.let(onNotifyOnTheWay) },
                    onStart = { order.id?.let(onStart) },
                    onComplete = { order.id?.let(onComplete) },
                )
            }
        }
        item { Spacer(Modifier.height(MainBottomNavInset)) }
    }
}

@Composable
private fun ActiveOrderRow(
    order: OrderListItem,
    isBusy: Boolean,
    onClick: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onStart: () -> Unit,
    onComplete: () -> Unit,
) {
    val status = order.orderStatus.toOrderStatus()
    // Per status: idle label + busy label + commit callback. The deliberate
    // swipe gesture (vs a one-tap button) is the point — advancing an
    // active job changes what the customer sees, so a mis-tap is a real
    // cost. Reuses the same SlideToCommit used in OrderDetail for the
    // Take/Start sliders so muscle memory is consistent.
    val swipeConfig: Triple<String, String, () -> Unit>? = when (status) {
        OrderStatus._2 -> Triple(
            stringResource(R.string.swipe_to_notify_on_the_way),
            stringResource(R.string.customer_notified_on_the_way),
            onNotifyOnTheWay,
        )
        OrderStatus._3 -> Triple(
            stringResource(R.string.swipe_to_start),
            stringResource(R.string.starting_order),
            onStart,
        )
        OrderStatus._4 -> Triple(
            stringResource(R.string.swipe_to_complete),
            stringResource(R.string.completing_order),
            onComplete,
        )
        else -> null
    }

    Surface(
        onClick = onClick,
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
    ) {
        Column(
            modifier = Modifier.padding(Spacing.M),
            verticalArrangement = Arrangement.spacedBy(Spacing.S),
        ) {
            // Top row: when + address + pay. Tap anywhere on this row
            // (whole card is clickable) opens the details screen.
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(Spacing.S),
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = order.cleaningDateTime?.let { formatTimeOnly(it) } ?: "—",
                        style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    )
                    Text(
                        text = order.customerAddress?.takeIf { it.isNotBlank() }
                            ?: order.customerName.orEmpty(),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
                Text(
                    text = formatMoney(order.estimatedCleanerPay ?: 0.0, order.currency?.symbol),
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.primary,
                )
                if (swipeConfig == null) {
                    Icon(
                        imageVector = Icons.AutoMirrored.Outlined.ArrowForward,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }

            // Swipe-to-act CTA per status. Inline busy state mirrors what
            // the OrderDetail slider does — thumb locks at the end with a
            // spinner while the API call is in flight; on failure the VM
            // clears inFlightActionOrderId and the thumb springs back so
            // the cleaner can retry.
            if (swipeConfig != null) {
                val (idle, busy, onCommit) = swipeConfig
                SlideToCommit(
                    idleLabel = idle,
                    busyLabel = busy,
                    onCommit = onCommit,
                    isBusy = isBusy,
                )
            }
        }
    }
}

// ─── History pane ───────────────────────────────────────────────────

@Composable
private fun HistoryPane(
    uiState: OrdersListUiState,
    onOrderClick: (String) -> Unit,
    onPeriodSelected: (CompletedPeriod) -> Unit,
) {
    val grouped = remember(uiState.orders) {
        uiState.orders.groupBy { it.cleaningLocalDate() }.toSortedMap(compareByDescending { it })
    }
    val totalEarnings = remember(uiState.orders) {
        uiState.orders.sumOf { (it.estimatedCleanerPay ?: 0.0) }
    }

    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(horizontal = Spacing.M, vertical = Spacing.S),
        verticalArrangement = Arrangement.spacedBy(Spacing.S),
    ) {
        item {
            PeriodFilterRow(
                currentPeriod = uiState.completedPeriod,
                onPeriodSelected = onPeriodSelected,
            )
        }
        item {
            HistorySummaryCard(
                totalEarnings = totalEarnings,
                currencySymbol = commonCurrencySymbol(uiState.orders),
                jobCount = uiState.orders.size,
            )
        }
        if (uiState.orders.isEmpty()) {
            item { EmptyInline(text = stringResource(R.string.no_completed_orders)) }
        }
        grouped.forEach { (date, ordersForDay) ->
            item {
                Text(
                    text = date?.let { formatDate(it.atStartOfDay(ZoneId.systemDefault()).toInstant().toString()) }
                        ?: stringResource(R.string.unscheduled),
                    style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = Spacing.S),
                )
            }
            items(ordersForDay, key = { it.id.orEmpty() }) { order ->
                HistoryRow(order = order, onClick = { order.id?.let(onOrderClick) })
            }
        }
        item { Spacer(Modifier.height(MainBottomNavInset)) }
    }
}

@Composable
private fun PeriodFilterRow(
    currentPeriod: CompletedPeriod,
    onPeriodSelected: (CompletedPeriod) -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .horizontalScroll(rememberScrollState()),
        horizontalArrangement = Arrangement.spacedBy(Spacing.S),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        CompletedPeriod.values().forEach { period ->
            val selected = period == currentPeriod
            Surface(
                onClick = { onPeriodSelected(period) },
                shape = RoundedCornerShape(50),
                color = if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.surface,
                border = BorderStroke(
                    1.dp,
                    if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                ),
            ) {
                Text(
                    text = stringResource(period.labelRes),
                    style = MaterialTheme.typography.labelLarge,
                    color = if (selected) MaterialTheme.colorScheme.onPrimary else MaterialTheme.colorScheme.onSurface,
                    fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Normal,
                    modifier = Modifier.padding(horizontal = 14.dp, vertical = 8.dp),
                )
            }
        }
    }
}

@Composable
private fun HistorySummaryCard(
    totalEarnings: Double,
    currencySymbol: String?,
    jobCount: Int,
) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(16.dp),
            ),
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface,
    ) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(Spacing.M),
            horizontalArrangement = Arrangement.SpaceAround,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            SummaryStat(label = stringResource(R.string.earnings), value = formatMoney(totalEarnings, currencySymbol))
            SummaryStat(label = stringResource(R.string.jobs), value = jobCount.toString())
        }
    }
}

@Composable
private fun SummaryStat(label: String, value: String) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text(
            text = value,
            style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.Bold),
            color = MaterialTheme.colorScheme.primary,
        )
        Text(
            text = label,
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun HistoryRow(order: OrderListItem, onClick: () -> Unit) {
    Surface(
        modifier = Modifier.fillMaxWidth().clickable { onClick() },
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
    ) {
        Row(
            modifier = Modifier.padding(horizontal = Spacing.M, vertical = Spacing.S),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(Spacing.S),
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = order.customerName?.takeIf { it.isNotBlank() } ?: stringResource(R.string.guest),
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
                Text(
                    text = order.cleaningDateTime?.let { formatTimeOnly(it) } ?: "—",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Text(
                text = formatMoney(order.estimatedCleanerPay ?: 0.0, order.currency?.symbol),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.primary,
            )
        }
    }
}

// ─── Shared bits ────────────────────────────────────────────────────

@Composable
private fun SearchBar(value: String, onValueChange: (String) -> Unit) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp),
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
    ) {
        Row(
            modifier = Modifier.padding(horizontal = Spacing.M, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                imageVector = Icons.Outlined.Search,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.width(8.dp))
            Box(modifier = Modifier.weight(1f)) {
                if (value.isEmpty()) {
                    Text(
                        text = stringResource(R.string.search_orders_hint),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                BasicTextField(
                    value = value,
                    onValueChange = onValueChange,
                    singleLine = true,
                    textStyle = MaterialTheme.typography.bodyMedium.copy(
                        color = MaterialTheme.colorScheme.onSurface,
                    ),
                    modifier = Modifier.fillMaxWidth(),
                )
            }
        }
    }
}

@Composable
private fun EmptyState(tab: OrdersTab) {
    val (mascot, text) = when (tab) {
        OrdersTab.Available -> R.drawable.mascot_resting to stringResource(R.string.no_orders_available)
        OrdersTab.MyActive -> R.drawable.mascot_thumbs_up to stringResource(R.string.no_active_orders)
        OrdersTab.MyCompleted -> R.drawable.mascot_thumbs_up to stringResource(R.string.no_completed_orders)
    }
    MascotEmptyState(
        painter = painterResource(mascot),
        text = text,
        modifier = Modifier.padding(bottom = MainBottomNavInset),
        // Orders has the segmented tab bar above the empty region; less
        // top spacer compensates so the mascot lands at the same Y as on
        // Invoices (no tabs).
        topSpacer = 220.dp,
    )
}

@Composable
private fun EmptyInline(text: String) {
    Text(
        text = text,
        style = MaterialTheme.typography.bodyMedium,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier.fillMaxWidth().padding(Spacing.L),
    )
}

// ─── Formatters ─────────────────────────────────────────────────────

private fun formatMoney(amount: Double, currencySymbol: String?): String {
    val rounded = amount.roundToInt()
    val whole = rounded.toString().reversed().chunked(3).joinToString(" ").reversed()
    val sym = currencySymbol?.trim().orEmpty()
    return if (sym.isEmpty()) whole else "$whole $sym"
}

private fun commonCurrencySymbol(orders: List<OrderListItem>): String? =
    orders.mapNotNull { it.currency?.symbol }.distinct().singleOrNull()

private fun formatDistance(km: Double): String = when {
    km < 1.0 -> String.format(Locale.getDefault(), "%.1f", km)
    else -> km.roundToInt().toString()
}

private fun formatDuration(minutes: Int): String {
    if (minutes < 60) return "$minutes min"
    val h = minutes / 60
    val m = minutes % 60
    return if (m == 0) "${h}h" else "${h}h ${m}m"
}

private fun formatRelativeDateTime(iso: String): String = runCatching {
    val instant = Instant.parse(iso)
    val local = instant.atZone(ZoneId.systemDefault())
    val date = local.toLocalDate()
    val today = java.time.LocalDate.now()
    val timeStr = DateTimeFormatter.ofPattern("HH:mm").format(local)
    when (date) {
        today -> "Today $timeStr"
        today.plusDays(1) -> "Tomorrow $timeStr"
        else -> DateTimeFormatter.ofPattern("EEE d MMM HH:mm")
            .withLocale(Locale.getDefault())
            .format(local)
    }
}.getOrDefault(iso)

private fun formatDate(iso: String): String = runCatching {
    val instant = Instant.parse(iso)
    val local = instant.atZone(ZoneId.systemDefault())
    DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM)
        .withLocale(Locale.getDefault())
        .format(local)
}.getOrDefault(iso)

private fun formatTimeOnly(iso: String): String = runCatching {
    val instant = Instant.parse(iso)
    val local = instant.atZone(ZoneId.systemDefault())
    DateTimeFormatter.ofPattern("HH:mm").format(local)
}.getOrDefault(iso)
