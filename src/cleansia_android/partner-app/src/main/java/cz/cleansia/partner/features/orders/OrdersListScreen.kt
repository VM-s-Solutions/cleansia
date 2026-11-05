package cz.cleansia.partner.features.orders

import android.text.format.DateFormat
import androidx.compose.animation.core.CubicBezierEasing
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListScope
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.ErrorOutline
import androidx.compose.material.icons.outlined.ExpandMore
import androidx.compose.material.icons.outlined.Payments
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material.icons.outlined.SwapVert
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.material3.pulltorefresh.rememberPullToRefreshState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.AlignmentLine
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.layout.Layout
import androidx.compose.ui.layout.layout
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.role
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.rememberTextMeasurer
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.money.CurrencySymbols
import cz.cleansia.core.ui.components.CleansiaButtonSize
import cz.cleansia.core.ui.components.CleansiaChip
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaTextButton
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.components.SudsRefreshIndicator
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.core.ui.theme.capBandAligned
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.data.orders.PendingOffer
import cz.cleansia.partner.data.payroll.CashHeld
import cz.cleansia.partner.features.main.MainBottomNavInset
import cz.cleansia.partner.ui.theme.Slate400
import cz.cleansia.partner.ui.theme.Slate500
import cz.cleansia.partner.ui.theme.pendingInk
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import kotlinx.coroutines.delay

/**
 * The cleaner's job board: one sliding tab switcher with counts over Available (reserved cards first, honest
 * cards, paging), Active (the hero, then Later) and History (period totals). Every state of every tab can be
 * pulled. -> /mobile-app/features#partner-orders
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrdersListScreen(
    onOrderClick: (String) -> Unit,
    onOpenLegalDocuments: () -> Unit,
    onOpenEarnings: () -> Unit = {},
    viewModel: OrdersListViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    var now by remember { mutableStateOf(Instant.now()) }
    LaunchedEffect(Unit) {
        while (true) {
            delay(30_000)
            now = Instant.now()
        }
    }
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { viewModel.onResume() }

    // Back to jobs: focus goes to the card that took the taken row's place, else the first, else the tab.
    val afterTakeFocus = remember { FocusRequester() }
    var focusIndexAfterTake by remember { mutableStateOf<Int?>(null) }

    uiState.contractRequest?.let { request ->
        WorkContractSheet(
            request = request,
            onDismiss = {
                if (uiState.takeTaken) focusIndexAfterTake = uiState.takenIndex ?: 0
                viewModel.dismissContract()
            },
            onOutcome = viewModel::onWorkContractOutcome,
            onOpenLegalDocuments = {
                viewModel.dismissContract()
                onOpenLegalDocuments()
            },
            taken = uiState.takeTaken,
            takenRow = uiState.takenRow,
            boardRefusal = uiState.takeRefusal,
            onOpenJob = {
                viewModel.dismissContract()
                onOrderClick(request.orderId)
            },
        )
    }

    var pendingDecline by remember { mutableStateOf<String?>(null) }
    pendingDecline?.let { orderId ->
        AlertDialog(
            onDismissRequest = { pendingDecline = null },
            title = { Text(stringResource(R.string.offer_decline_title)) },
            text = { Text(stringResource(R.string.offer_decline_body)) },
            confirmButton = {
                TextButton(
                    onClick = {
                        pendingDecline = null
                        viewModel.declineOffer(orderId)
                    },
                    colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
                ) { Text(stringResource(R.string.offer_decline_cta)) }
            },
            dismissButton = { CleansiaTextButton(onClick = { pendingDecline = null }) { Text(stringResource(R.string.cancel)) } },
        )
    }
    uiState.declineRefusal?.let { reason ->
        val copy = offerRefusalCopy(OfferAction.Decline)
        AlertDialog(
            onDismissRequest = viewModel::dismissDeclineRefusal,
            title = { Text(stringResource(copy.titleRes)) },
            text = { Text(stringResource(copy.bodyRes, reason)) },
            confirmButton = { CleansiaTextButton(onClick = viewModel::dismissDeclineRefusal) { Text(stringResource(R.string.ok)) } },
        )
    }

    OrdersListContent(
        uiState = uiState,
        now = now,
        onSelectTab = viewModel::selectTab,
        onRefresh = viewModel::onRefresh,
        onOrderClick = onOrderClick,
        onTake = viewModel::takeOrderInline,
        onDecline = { pendingDecline = it },
        onSortSelected = viewModel::setAvailableSort,
        onLoadMore = viewModel::loadMore,
        onNotifyOnTheWay = viewModel::notifyOnTheWayInline,
        onStart = viewModel::startOrderInline,
        onPeriodSelected = viewModel::setCompletedPeriod,
        onOpenEarnings = onOpenEarnings,
        afterTakeFocus = afterTakeFocus,
        focusIndexAfterTake = focusIndexAfterTake,
        onAfterTakeFocused = { focusIndexAfterTake = null },
    )
}

/** The board as a function of its state: everything the screen draws, with no view model or sheet. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrdersListContent(
    uiState: OrdersListUiState,
    now: Instant,
    onSelectTab: (OrdersTab) -> Unit,
    onRefresh: () -> Unit,
    onOrderClick: (String) -> Unit,
    onTake: (String) -> Unit,
    onDecline: (String) -> Unit,
    onSortSelected: (AvailableSort) -> Unit,
    onLoadMore: () -> Unit,
    onNotifyOnTheWay: (String) -> Unit,
    onStart: (String) -> Unit,
    onPeriodSelected: (CompletedPeriod) -> Unit,
    onOpenEarnings: () -> Unit,
    afterTakeFocus: FocusRequester = remember { FocusRequester() },
    focusIndexAfterTake: Int? = null,
    onAfterTakeFocused: () -> Unit = {},
) {
    val statusBarTop = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()
    val tabFocus = remember { FocusRequester() }
    val available = uiState.pane(OrdersTab.Available)
    val lists = OrdersTab.entries.associateWith { rememberLazyListState() }
    val list = lists.getValue(uiState.tab)
    val scrolled by remember(list) { derivedStateOf { list.canScrollBackward } }
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
            modifier = Modifier.padding(start = Spacing.M, end = Spacing.M, top = Spacing.S),
        )
        SegmentedTabs(
            current = uiState.tab,
            counts = mapOf(
                OrdersTab.Available to OrdersBoardRules.availableBadge(available.hasLoaded, available.total, available.rows, now),
                OrdersTab.MyActive to uiState.pane(OrdersTab.MyActive).total,
            ),
            onSelect = onSelectTab,
            availableFocus = tabFocus,
            modifier = Modifier.padding(start = Spacing.M, end = Spacing.M, top = Spacing.XS, bottom = 7.dp),
        )
        HorizontalDivider(color = if (scrolled) MaterialTheme.colorScheme.outlineVariant else Color.Transparent)

        val pullState = rememberPullToRefreshState()
        PullToRefreshBox(
            // Only a user pull drives the indicator; background refreshes never do.
            isRefreshing = uiState.isUserRefreshing,
            onRefresh = onRefresh,
            state = pullState,
            modifier = Modifier.fillMaxSize(),
            indicator = {
                SudsRefreshIndicator(
                    state = pullState,
                    isRefreshing = uiState.isUserRefreshing,
                    modifier = Modifier
                        .align(Alignment.TopCenter)
                        .padding(top = 8.dp),
                )
            },
        ) {
            when (uiState.tab) {
                OrdersTab.Available -> AvailablePane(
                    uiState = uiState,
                    now = now,
                    list = list,
                    onOrderClick = onOrderClick,
                    onTake = onTake,
                    onDecline = onDecline,
                    onSortSelected = onSortSelected,
                    onRetry = onRefresh,
                    onLoadMore = onLoadMore,
                    afterTakeFocus = afterTakeFocus,
                    focusIndexAfterTake = focusIndexAfterTake,
                    onAfterTakeFocused = onAfterTakeFocused,
                    tabFocus = tabFocus,
                )
                OrdersTab.MyActive -> ActivePane(
                    uiState = uiState,
                    now = now,
                    list = list,
                    onOrderClick = onOrderClick,
                    onNotifyOnTheWay = onNotifyOnTheWay,
                    onStart = onStart,
                    onRetry = onRefresh,
                    onShowAvailable = { onSelectTab(OrdersTab.Available) },
                )
                OrdersTab.MyCompleted -> HistoryPane(
                    uiState = uiState,
                    now = now,
                    list = list,
                    onOrderClick = onOrderClick,
                    onPeriodSelected = onPeriodSelected,
                    onRetry = onRefresh,
                    onOpenEarnings = onOpenEarnings,
                )
            }
        }
    }
}

/** A tab pane's list: 16 from the screen, the first card 8 under the header, cards 12 apart. */
private val PanePadding = PaddingValues(start = Spacing.M, end = Spacing.M, top = Spacing.XS)

// ─── Tab switcher ──────────────────────────────────────────────────

/**
 * One capsule track with a sliding knob and three segments, each a tab in a selectable group that reports whether it
 * is the selected one and says its count ("Available, 6 jobs") whatever arrangement fits. The segments share the track
 * equally while each keeps 12 between its words and the knob's edge; a longer one takes the room it needs from the
 * others. Badge beside the label; at large text under it; with no room, the label alone (TalkBack still says the count).
 */
@Composable
private fun SegmentedTabs(
    current: OrdersTab,
    counts: Map<OrdersTab, Int?>,
    onSelect: (OrdersTab) -> Unit,
    availableFocus: FocusRequester,
    modifier: Modifier = Modifier,
) {
    val dark = isDarkScheme()
    val trackColor = if (dark) MaterialTheme.colorScheme.surface else MaterialTheme.colorScheme.outline
    val knobColor = if (dark) MaterialTheme.colorScheme.outline else MaterialTheme.colorScheme.surface
    val density = LocalDensity.current
    val large = density.fontScale >= 1.3f
    val measurer = rememberTextMeasurer()
    val labelStyle = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold)
    val badgeStyle = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold, fontFeatureSettings = "tnum")
    val tabs = OrdersTab.entries
    val labels = tabs.map { stringResource(segmentLabelFor(it)) }
    val badges = tabs.map { OrdersBoardRules.badge(counts[it]) }
    BoxWithConstraints(
        modifier = modifier
            .fillMaxWidth()
            .background(trackColor, CircleShape)
            .padding(TRACK_PADDING),
    ) {
        val inner = constraints.maxWidth
        val px = { dp: Dp -> with(density) { dp.roundToPx() } }
        val labelWidths = labels.map { measurer.measure(it, labelStyle, maxLines = 1, softWrap = false).size.width }
        val badgeWidths = badges.map { badge -> badge?.let { maxOf(px(BadgeSize), measurer.measure(it, badgeStyle, maxLines = 1, softWrap = false).size.width + px(Spacing.XS) * 2 - px(2.dp)) } ?: 0 }
        val edge = px(SEGMENT_LABEL_INSET) * 2
        val beside = tabs.indices.map { labelWidths[it] + (if (badgeWidths[it] > 0) px(BadgeGap) + badgeWidths[it] else 0) + edge }
        val stacked = tabs.indices.map { maxOf(labelWidths[it], badgeWidths[it]) + edge }
        val arrangement = when {
            !large && beside.sum() <= inner -> SegmentArrangement.Beside
            stacked.sum() <= inner -> SegmentArrangement.Stacked
            else -> SegmentArrangement.LabelAlone
        }
        val need = when (arrangement) {
            SegmentArrangement.Beside -> beside
            SegmentArrangement.Stacked -> stacked
            SegmentArrangement.LabelAlone -> labelWidths.map { it + edge }
        }
        val widths = segmentWidths(need, inner)
        val index = tabs.indexOf(current)
        val easing = tween<Dp>(durationMillis = 260, easing = CubicBezierEasing(0.22f, 1f, 0.36f, 1f))
        val knobX by animateDpAsState(with(density) { widths.take(index).sum().toDp() }, easing, label = "orders-tab-knob")
        val knobWidth by animateDpAsState(with(density) { widths[index].toDp() }, easing, label = "orders-tab-knob-width")
        Box(Modifier.fillMaxWidth().height(IntrinsicSize.Min)) {
            Box(
                Modifier
                    .offset { IntOffset(knobX.roundToPx(), 0) }
                    .width(knobWidth)
                    .fillMaxHeight()
                    .background(knobColor, CircleShape),
            )
            Row(modifier = Modifier.fillMaxWidth().selectableGroup()) {
                tabs.forEachIndexed { i, tab ->
                    SegmentChip(
                        label = labels[i],
                        badge = badges[i],
                        count = counts[tab],
                        selected = tab == current,
                        arrangement = arrangement,
                        onClick = { onSelect(tab) },
                        modifier = Modifier
                            .width(with(density) { widths[i].toDp() })
                            .fillMaxHeight()
                            .then(if (tab == OrdersTab.Available) Modifier.focusRequester(availableFocus) else Modifier),
                    )
                }
            }
        }
    }
}

private enum class SegmentArrangement { Beside, Stacked, LabelAlone }

/**
 * Each segment's width in an [inner]-wide track: an equal share, and a segment that needs more than its share takes what
 * it needs from the others. With no room for every need, each takes its need.
 */
internal fun segmentWidths(need: List<Int>, inner: Int): List<Int> {
    val fixed = BooleanArray(need.size)
    while (true) {
        val free = need.indices.filter { !fixed[it] }
        if (free.isEmpty()) return need
        val room = inner - need.indices.filter { fixed[it] }.sumOf { need[it] }
        val share = room / free.size
        val over = free.filter { need[it] > share }
        if (over.isEmpty()) {
            val extra = room - share * free.size
            return need.indices.map { i -> if (fixed[i]) need[i] else share + if (i == free.last()) extra else 0 }
        }
        over.forEach { fixed[it] = true }
    }
}

private fun segmentLabelFor(tab: OrdersTab): Int = when (tab) {
    OrdersTab.Available -> R.string.available
    OrdersTab.MyActive -> R.string.active
    OrdersTab.MyCompleted -> R.string.history
}

/** The track's padding around the knob. */
private val TRACK_PADDING = 3.dp

/** Clear room between a segment's words and the knob's edge, so they never reach its round ends. */
private val SEGMENT_LABEL_INSET = 12.dp

private val BadgeSize = 22.dp
private val BadgeGap = 6.dp

@Composable
private fun SegmentChip(
    label: String,
    badge: String?,
    count: Int?,
    selected: Boolean,
    arrangement: SegmentArrangement,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val spoken = count?.takeIf { it > 0 }?.let { "$label, ${pluralStringResource(R.plurals.orders_job_count, it, it)}" } ?: label
    Surface(
        selected = selected,
        onClick = onClick,
        modifier = modifier
            .heightIn(min = 42.dp)
            .semantics {
                role = Role.Tab
                contentDescription = spoken
            },
        shape = CircleShape,
        color = Color.Transparent,
    ) {
        val words: @Composable () -> Unit = {
            Text(
                text = label,
                style = MaterialTheme.typography.labelLarge.copy(fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Normal),
                color = if (selected) MaterialTheme.colorScheme.onSurface else MaterialTheme.colorScheme.onSurfaceVariant,
                maxLines = if (arrangement == SegmentArrangement.LabelAlone) 2 else 1,
                softWrap = arrangement == SegmentArrangement.LabelAlone,
                textAlign = TextAlign.Center,
            )
        }
        Box(contentAlignment = Alignment.Center, modifier = Modifier.padding(horizontal = SEGMENT_LABEL_INSET, vertical = 2.dp)) {
            when {
                badge == null || arrangement == SegmentArrangement.LabelAlone -> words()
                arrangement == SegmentArrangement.Beside -> Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(BadgeGap)) {
                    words()
                    TabBadge(badge)
                }
                else -> Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(2.dp), modifier = Modifier.padding(vertical = 4.dp)) {
                    words()
                    TabBadge(badge)
                }
            }
        }
    }
}

@Composable
private fun TabBadge(text: String) {
    Box(
        modifier = Modifier
            .heightIn(min = BadgeSize)
            .widthIn(min = BadgeSize)
            .background(tonalFill(), CircleShape)
            .padding(horizontal = 6.dp),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold, fontFeatureSettings = "tnum"),
            color = primaryText(),
            maxLines = 1,
        )
    }
}

/** The app's card: the surface, radius 16, a hairline in the outline variant; [padded] puts its words 16 in. */
@Composable
internal fun Panel(modifier: Modifier = Modifier, padded: Boolean = false, content: @Composable ColumnScope.() -> Unit) {
    Column(
        modifier = modifier
            .fillMaxWidth()
            .clip(MaterialTheme.shapes.medium)
            .background(MaterialTheme.colorScheme.surface)
            .border(BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant), MaterialTheme.shapes.medium)
            .then(if (padded) Modifier.padding(Spacing.M) else Modifier),
        content = content,
    )
}

@Composable
internal fun Rule(modifier: Modifier = Modifier) = HorizontalDivider(modifier = modifier, color = MaterialTheme.colorScheme.outlineVariant)

/** A flat bar at the real size of the value it stands for: the skeleton, with no shimmer. */
@Composable
internal fun Bar(width: Dp, height: Dp, modifier: Modifier = Modifier) {
    Box(modifier.size(width, height).background(MaterialTheme.colorScheme.surfaceVariant, RoundedCornerShape(6.dp)))
}

/** "Later · 2 jobs": a heading on the ground over its card, the count in the secondary ink. */
@Composable
internal fun SectionHeading(title: String, count: Int? = null) {
    Row(
        modifier = Modifier.padding(top = Spacing.XS).semantics(mergeDescendants = true) { heading() },
        horizontalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        Text(
            text = OrdersBoardRules.tie(title),
            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onBackground,
            modifier = Modifier.alignByBaseline(),
        )
        if (count != null) {
            Text(
                text = "· " + pluralStringResource(R.plurals.orders_job_count, count, count),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.alignByBaseline(),
            )
        }
    }
}

/** An outlined button in the brand: the in-card secondary move (Retry, Check again, See all, Open the job). */
@Composable
internal fun BrandOutlinedButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    leadingIcon: androidx.compose.ui.graphics.vector.ImageVector? = null,
    enabled: Boolean = true,
    maxLines: Int = 1,
) {
    CleansiaOutlinedButton(
        text = text,
        onClick = onClick,
        size = CleansiaButtonSize.Medium,
        leadingIcon = leadingIcon,
        enabled = enabled,
        maxLines = maxLines,
        contentColor = primaryText(),
        borderColor = MaterialTheme.colorScheme.primary,
        modifier = modifier,
    )
}

/** A short state's card: the answer in words beside a mascot, then the one move under them. */
@Composable
private fun StatePanel(
    title: String,
    body: String,
    mascot: Int,
    mascotSize: Dp,
    action: String,
    onAction: () -> Unit,
    titleIcon: Boolean = false,
) {
    val large = LocalDensity.current.fontScale >= 1.3f
    Panel(padded = true) {
        val words: @Composable () -> Unit = {
            Column {
                Row {
                    val style = MaterialTheme.typography.titleMedium
                    if (titleIcon) Icon(Icons.Outlined.ErrorOutline, contentDescription = null, tint = MaterialTheme.colorScheme.error, modifier = capBandAligned(style).padding(end = 8.dp).size(18.dp))
                    Text(
                        text = OrdersBoardRules.tie(title),
                        style = style,
                        color = MaterialTheme.colorScheme.onSurface,
                        modifier = Modifier.alignBy(FirstBaseline).semantics { heading() }.announcePolitely(),
                    )
                }
                Text(
                    text = OrdersBoardRules.tie(body),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = Spacing.XXS),
                )
            }
        }
        if (large) {
            words()
        } else {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(Spacing.S)) {
                Box(Modifier.weight(1f)) { words() }
                Image(painter = painterResource(mascot), contentDescription = null, modifier = Modifier.size(mascotSize))
            }
        }
        BrandOutlinedButton(text = action, onClick = onAction, leadingIcon = Icons.Outlined.Refresh, modifier = Modifier.padding(top = Spacing.S))
    }
}

internal enum class AvailableShort { Error, Empty }

/** Available's short panel, from what is on show: the error only while a failed pane holds no row at all, else empty once no card is left. */
internal fun availableShort(pane: PaneState, cards: List<OrderListItem>, reservedShown: Boolean): AvailableShort? = when {
    pane.showsError(heldElsewhere = reservedShown) -> AvailableShort.Error
    cards.isEmpty() && !reservedShown -> AvailableShort.Empty
    else -> null
}

@Composable
private fun AvailablePane(
    uiState: OrdersListUiState,
    now: Instant,
    list: LazyListState,
    onOrderClick: (String) -> Unit,
    onTake: (String) -> Unit,
    onDecline: (String) -> Unit,
    onSortSelected: (AvailableSort) -> Unit,
    onRetry: () -> Unit,
    onLoadMore: () -> Unit,
    afterTakeFocus: FocusRequester,
    focusIndexAfterTake: Int?,
    onAfterTakeFocused: () -> Unit,
    tabFocus: FocusRequester,
) {
    val rules = OrdersBoardRules
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val today = now.atZone(zone).toLocalDate()
    val pane = uiState.pane(OrdersTab.Available)
    val active = uiState.pane(OrdersTab.MyActive)
    // The clash check runs only on a complete, current Active list; otherwise the server stays the guard.
    val own = if (active.hasLoaded && !active.failed && active.complete) active.rows else emptyList()
    val reserved = remember(uiState.offers, pane.rows, uiState.reservedRows, now) {
        rules.reservedShown(uiState.offers, pane.rows + uiState.reservedRows.values, now)
    }
    val reservedIds = reserved.map { it.second.id }.toSet()
    val sortOptions = rules.sortOptions(pane.rows.size, pane.total)
    val sort = uiState.availableSort.takeIf { it in sortOptions } ?: AvailableSort.SoonestFirst
    val cards = remember(pane.rows, reservedIds, sort, now) { rules.visibleCards(pane.rows, reservedIds, sort, now) }
    val clashes = remember(cards, reserved, own) { (cards + reserved.map { it.second }).associate { it.id to rules.clashes(it, own) } }
    val clashing = (reserved.map { it.second } + cards).mapNotNull { row -> clashes[row.id]?.let { row to it } }
    val finding = rules.overlapFinding(clashing, today, zone, locale)?.text()
    val groups = remember(cards, zone) { rules.dayGroups(cards, zone) }
    val range = remember(cards, reserved, own, zone) {
        rules.dayRange(
            (cards + reserved.map { it.second } + own).mapNotNull { row -> rules.day(row, zone)?.let { rules.daySpan(row, it, zone) } },
        )
    }
    val tileWidth = payTileWidth(reserved.map { reservedTileSpec(it.second) })
    val appendedFocus = remember { FocusRequester() }
    LaunchedEffect(pane.appendedFrom) {
        if (pane.appendedFrom != null) runCatching { delay(50); appendedFocus.requestFocus() }
    }
    LaunchedEffect(focusIndexAfterTake) {
        if (focusIndexAfterTake == null) return@LaunchedEffect
        delay(300)
        runCatching { if (cards.isNotEmpty()) afterTakeFocus.requestFocus() else tabFocus.requestFocus() }
        onAfterTakeFocused()
    }
    val headerMode = dayHeaderMode(groups.mapNotNull { it.first }.map { day -> day to rules.ownOfDay(own, day, zone) }, today)
    val short = availableShort(pane, cards, reservedShown = reserved.isNotEmpty())

    LazyColumn(
        state = list,
        modifier = Modifier.fillMaxSize(),
        contentPadding = PanePadding,
        verticalArrangement = Arrangement.spacedBy(Spacing.S),
    ) {
        when {
            !pane.hasLoaded && !pane.failed -> item { AvailableLoading() }
            short != null -> {
                val error = short == AvailableShort.Error
                item {
                    StatePanel(
                        title = stringResource(if (error) R.string.orders_load_error else R.string.available_empty_title),
                        body = if (error) {
                            stringResource(R.string.available_empty_subtitle)
                        } else {
                            pane.loadedAt?.let { stringResource(R.string.available_checked_at, rules.timeText(it, zone, locale)) + ". " }.orEmpty() +
                                stringResource(R.string.available_empty_subtitle)
                        },
                        mascot = if (error) R.drawable.mascot_resting else R.drawable.mascot_leaning,
                        mascotSize = 112.dp,
                        action = stringResource(if (error) R.string.retry else R.string.orders_check_again),
                        onAction = onRetry,
                    )
                }
                uiState.cashHidden?.let { item { CashNotice(it) } }
                nextJobs(active, now, onOrderClick)
                if (uiState.cashHidden == null) uiState.cashHeld?.let { item { CashPanel(it) } }
            }
            else -> {
                if (pane.failed) item(key = "stale") { StaleStrip(loadedAt = pane.loadedAt, now = now, onRetry = onRetry) }
                items(reserved, key = { "reserved-" + it.first.id }) { (offer, row) ->
                    ReservedCard(
                        row = row,
                        offer = offer,
                        now = now,
                        clash = clashes[row.id],
                        declineBusy = uiState.decliningOrderId == offer.id,
                        onOpen = { row.id?.let(onOrderClick) },
                        onConfirm = { row.id?.let(onTake) },
                        onDecline = { onDecline(offer.id) },
                        tileWidth = tileWidth,
                        modifier = Modifier.animateItem(),
                    )
                }
                item(key = "toolbar") {
                    AvailableToolbar(
                        cashHidden = uiState.cashHidden != null,
                        overlap = finding,
                        sortOptions = sortOptions,
                        currentSort = sort,
                        onSortSelected = onSortSelected,
                    )
                }
                uiState.cashHidden?.let { item(key = "cash-notice") { CashNotice(it) } }
                groups.forEach { (day, rows) ->
                    item(key = "day-${day ?: "none"}-${rows.first().id}") {
                        DayPanel(
                            day = day,
                            rows = rows,
                            own = own,
                            reservedRows = reserved.map { it.second },
                            range = range,
                            headerMode = headerMode,
                            now = now,
                            clashes = clashes,
                            onOrderClick = onOrderClick,
                            focusFor = { _, row ->
                                when {
                                    pane.appendedFrom != null && row.id == pane.rows.getOrNull(pane.appendedFrom)?.id -> appendedFocus
                                    focusIndexAfterTake != null && cards.indexOf(row) == focusIndexAfterTake.coerceAtMost(cards.lastIndex) -> afterTakeFocus
                                    else -> null
                                }
                            },
                            modifier = Modifier.animateItem(),
                        )
                    }
                }
                item(key = "footer") { AvailableFooter(pane = pane, onLoadMore = onLoadMore) }
                if ((cards + reserved.map { it.second }).any { it.homeDistanceBand != null }) {
                    item(key = "band-footnote") {
                        Text(
                            text = OrdersBoardRules.tie(stringResource(R.string.available_distance_footnote)),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.fillMaxWidth(),
                        )
                    }
                }
            }
        }
        item { Spacer(Modifier.height(MainBottomNavInset)) }
    }
}

/** How every day header of the page shows her jobs, decided once: the long label, the short word, or under the name. */
private enum class HeaderMode { Long, Short, Wrap }

@Composable
private fun dayHeaderMode(days: List<Pair<LocalDate, List<OrderListItem>>>, today: LocalDate): HeaderMode {
    val rules = OrdersBoardRules
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val density = LocalDensity.current
    val measurer = rememberTextMeasurer()
    val nameStyle = MaterialTheme.typography.titleMedium
    val labelStyle = MaterialTheme.typography.bodyMedium.copy(fontFeatureSettings = "tnum")
    val resources = androidx.compose.ui.platform.LocalContext.current.resources
    val mine = days.filter { it.second.isNotEmpty() }
    if (mine.isEmpty()) return HeaderMode.Long
    val screen = androidx.compose.ui.platform.LocalConfiguration.current.screenWidthDp.dp
    val leaf = maxOf(TileSize.Header.minSide, 40.dp * density.fontScale + 6.dp)
    val room = with(density) { (screen - Spacing.M * 2 - Spacing.M * 2 - 2.dp - leaf - Spacing.S - Spacing.XS - MineMarkRoom).roundToPx() }
    val fits = { short: Boolean ->
        mine.all { (day, own) ->
            val name = measurer.measure(rules.dayName(day, today, locale).resolve(resources), nameStyle, softWrap = false, maxLines = 1).size.width
            val label = measurer.measure(rules.dayMineLabel(own, short, zone, locale)!!.resolve(resources), labelStyle, softWrap = false, maxLines = 1).size.width
            name + label <= room
        }
    }
    val long = fits(false)
    val short = long || fits(true)
    return when {
        long -> HeaderMode.Long
        short -> HeaderMode.Short
        else -> HeaderMode.Wrap
    }
}

/** Her job's mark before its label: the 8 dot and its 6 gap. */
private val MineMarkRoom = 14.dp

/** One day: leaf │ the day's name and her job that day │ the day line; then its job rows between rules. */
@Composable
private fun DayPanel(
    day: LocalDate?,
    rows: List<OrderListItem>,
    own: List<OrderListItem>,
    reservedRows: List<OrderListItem>,
    range: DayRange,
    headerMode: HeaderMode,
    now: Instant,
    clashes: Map<String?, OrderListItem?>,
    onOrderClick: (String) -> Unit,
    focusFor: (Int, OrderListItem) -> FocusRequester?,
    modifier: Modifier = Modifier,
) {
    val rules = OrdersBoardRules
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val today = now.atZone(zone).toLocalDate()
    Panel(modifier = modifier) {
        if (day != null) {
            val mine = rules.ownOfDay(own, day, zone)
            val name = rules.dayName(day, today, locale).text()
            val label = rules.dayMineLabel(mine, headerMode != HeaderMode.Long, zone, locale)?.text()
            val open = rows + reservedRows.filter { rules.day(it, zone) == day }
            Row(
                modifier = Modifier
                    .padding(horizontal = Spacing.M, vertical = Spacing.S)
                    .semantics(mergeDescendants = true) { heading() },
                horizontalArrangement = Arrangement.spacedBy(Spacing.S),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                OrdersDateTile(start = rules.start(rows.first()), size = TileSize.Header, now = now)
                Column(Modifier.weight(1f)) {
                    val nameText: @Composable (Modifier) -> Unit = { m ->
                        Text(text = name, style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.onSurface, maxLines = 1, modifier = m)
                    }
                    val labelText: @Composable (Modifier) -> Unit = { m -> if (label != null) MineLabel(label, m) }
                    if (headerMode == HeaderMode.Wrap) {
                        nameText(Modifier)
                        labelText(Modifier)
                    } else {
                        Row {
                            nameText(Modifier.weight(1f).alignBy(FirstBaseline))
                            labelText(Modifier.alignBy(FirstBaseline))
                        }
                    }
                    OrdersDayLine(
                        range = range,
                        mine = mine.mapNotNull { rules.daySpan(it, day, zone) },
                        open = rules.openSlots(open, own, day, zone),
                        now = if (day == today) now.atZone(zone).let { it.hour * 60 + it.minute } else null,
                        modifier = Modifier.padding(top = 6.dp),
                    )
                }
            }
        }
        rows.forEachIndexed { index, row ->
            Rule()
            AvailableJobRow(
                row = row,
                now = now,
                clash = clashes[row.id],
                onOpen = { row.id?.let(onOrderClick) },
                bodyFocus = focusFor(index, row),
            )
        }
    }
}

/** "● your job 14:00–16:00": her own time that day, the dot on the words' x-height. */
@Composable
private fun MineLabel(label: String, modifier: Modifier = Modifier) {
    val style = MaterialTheme.typography.bodyMedium.copy(fontFeatureSettings = "tnum")
    Row(modifier) {
        Box(xBandAligned(style).size(8.dp).background(MaterialTheme.colorScheme.primary, CircleShape))
        Text(text = label, style = style, color = primaryText(), maxLines = 1, softWrap = false, modifier = Modifier.padding(start = 6.dp).alignBy(FirstBaseline))
    }
}

/**
 * The finding at the start (cash hidden in amber, else the overlap with a red mark), the sort at the end on its first
 * line; when the finding wraps, the sort is its glyph alone.
 */
@Composable
private fun AvailableToolbar(
    cashHidden: Boolean,
    overlap: String?,
    sortOptions: List<AvailableSort>,
    currentSort: AvailableSort,
    onSortSelected: (AvailableSort) -> Unit,
) {
    val text = when {
        cashHidden -> stringResource(R.string.available_cash_hidden)
        else -> overlap
    }
    val style = MaterialTheme.typography.bodyMedium
    val linkStyle = MaterialTheme.typography.labelLarge
    BoxWithConstraints(Modifier.fillMaxWidth().padding(vertical = Spacing.XXS)) {
        val measurer = rememberTextMeasurer()
        val density = LocalDensity.current
        val sortWidth = measurer.measure(stringResource(currentSort.labelRes), linkStyle, softWrap = false).size.width +
            with(density) { (if (sortOptions.size > 1) 18.dp else 0.dp).roundToPx() }
        val findingRoom = constraints.maxWidth - sortWidth - with(density) { (Spacing.S + 18.dp + Spacing.XS).roundToPx() }
        val compact = text != null && measurer.measure(OrdersBoardRules.tie(text), style, constraints = Constraints(maxWidth = findingRoom.coerceAtLeast(1))).lineCount > 1
        val sort: @Composable () -> Unit = { SortControl(options = sortOptions, currentSort = currentSort, onSortSelected = onSortSelected, compact = compact) }
        if (text == null) {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) { sort() }
        } else {
            ToolbarLayout(
                finding = {
                    Row {
                        Icon(
                            imageVector = if (cashHidden) Icons.Outlined.Payments else Icons.Outlined.ErrorOutline,
                            contentDescription = null,
                            tint = if (cashHidden) MaterialTheme.colorScheme.pendingInk else MaterialTheme.colorScheme.error,
                            modifier = capBandAligned(style).size(18.dp),
                        )
                        Text(
                            text = OrdersBoardRules.tie(text),
                            style = style,
                            color = if (cashHidden) MaterialTheme.colorScheme.pendingInk else MaterialTheme.colorScheme.onSurface,
                            modifier = Modifier.padding(start = Spacing.XS).alignBy(FirstBaseline),
                        )
                    }
                },
                sort = sort,
                capBand = with(density) { (style.fontSize.toPx() * cz.cleansia.core.ui.theme.NunitoCapHeight / 2f).toInt() },
            )
        }
    }
}

/**
 * The finding and the sort on one first line: a sort with words shares the finding's first baseline, a glyph alone sits on
 * its cap band ([capBand] above the baseline). The sort's touch room reaches past the line.
 */
@Composable
private fun ToolbarLayout(finding: @Composable () -> Unit, sort: @Composable () -> Unit, capBand: Int) {
    Layout(contents = listOf(finding, sort)) { (findingM, sortM), constraints ->
        val width = constraints.maxWidth
        val sortP = sortM.first().measure(Constraints())
        val findingP = findingM.first().measure(Constraints(maxWidth = (width - sortP.width - Spacing.S.roundToPx()).coerceAtLeast(0)))
        val base = findingP[FirstBaseline].takeIf { it != AlignmentLine.Unspecified } ?: 0
        val sortBase = sortP[FirstBaseline].takeIf { it != AlignmentLine.Unspecified }
        val sortY = if (sortBase != null) base - sortBase else base - capBand - sortP.height / 2
        layout(width, findingP.height) {
            findingP.place(0, 0)
            sortP.place(width - sortP.width, sortY)
        }
    }
}

/** One option: plain words, not a button. Two: a menu whose label and chevron read in the text blue. */
@Composable
private fun SortControl(options: List<AvailableSort>, currentSort: AvailableSort, onSortSelected: (AvailableSort) -> Unit, compact: Boolean) {
    val style = MaterialTheme.typography.labelLarge
    val spoken = stringResource(R.string.orders_sort_a11y, stringResource(currentSort.labelRes))
    if (options.size < 2) {
        Box(Modifier.heightIn(min = 48.dp).semantics { contentDescription = spoken }, contentAlignment = Alignment.Center) {
            if (compact) {
                Icon(Icons.Outlined.SwapVert, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.size(20.dp))
            } else {
                Text(text = stringResource(R.string.sort_soonest_first), style = style, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        return
    }
    var expanded by remember { mutableStateOf(false) }
    Box {
        Row(
            modifier = Modifier
                .heightIn(min = 48.dp)
                .widthIn(min = 48.dp)
                .clip(CircleShape)
                .clickable { expanded = true }
                .semantics { contentDescription = spoken; role = Role.DropdownList }
                .padding(horizontal = Spacing.XXS),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.Center,
        ) {
            if (compact) {
                Icon(imageVector = Icons.Outlined.SwapVert, contentDescription = null, tint = primaryText(), modifier = Modifier.size(20.dp))
            } else {
                Row {
                    Text(text = stringResource(currentSort.labelRes), style = style, color = primaryText(), modifier = Modifier.alignBy(FirstBaseline))
                    Icon(
                        imageVector = Icons.Outlined.ExpandMore,
                        contentDescription = null,
                        tint = primaryText(),
                        modifier = capBandAligned(style).padding(start = 2.dp).size(16.dp),
                    )
                }
            }
        }
        DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            options.forEach { option ->
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
 * Over the limit (cash jobs hidden): the held amount over the bar, amber up to an ink tick at the limit and red
 * past it, *0* at the left end and the limit at the tick; then how the jobs come back.
 */
@Composable
private fun CashNotice(cash: CashHeld) {
    val locale = appLocale()
    val symbol = CurrencySymbols.forCode(cash.currencyCode)
    val held = OrdersBoardRules.money(cash.amount, symbol, locale)
    val cap = cash.floatCap
    val limit = cap?.let { OrdersBoardRules.money(it, symbol, locale) }
    val ink = MaterialTheme.colorScheme.pendingInk
    val title = MaterialTheme.typography.titleMedium
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(amberFill(), MaterialTheme.shapes.medium)
            .padding(horizontal = Spacing.M, vertical = Spacing.S),
    ) {
        Icon(Icons.Outlined.Payments, contentDescription = null, tint = ink, modifier = capBandAligned(title).size(18.dp))
        Column(Modifier.weight(1f).padding(start = 10.dp).alignBy(FirstBaseline)) {
            Row {
                Text(text = stringResource(R.string.cash_held_title), style = title, color = ink, modifier = Modifier.weight(1f).alignByBaseline())
                Text(text = held, style = title.copy(fontFeatureSettings = "tnum"), color = ink, modifier = Modifier.alignByBaseline())
            }
            if (cap != null && limit != null && cash.amount > 0) {
                val tick = (cap / cash.amount).toFloat().coerceIn(0f, 1f)
                CashMeter(fill = tick, over = true, track = MaterialTheme.colorScheme.surface, modifier = Modifier.padding(top = 6.dp))
                val small = MaterialTheme.typography.bodySmall.copy(fontFeatureSettings = "tnum")
                BoxWithConstraints(Modifier.fillMaxWidth()) {
                    Text(text = "0", style = small, color = ink)
                    val label = stringResource(R.string.cash_limit_at, limit)
                    val measurer = rememberTextMeasurer()
                    val labelWidth = measurer.measure(label, small, softWrap = false).size.width
                    val x = (constraints.maxWidth * tick - labelWidth).coerceAtLeast(0f)
                    Text(text = label, style = small, color = ink, softWrap = false, modifier = Modifier.offset { IntOffset(x.toInt(), 0) })
                }
                Text(
                    text = OrdersBoardRules.tie(stringResource(R.string.cash_hidden_back, limit)),
                    style = MaterialTheme.typography.bodyMedium,
                    color = ink,
                    modifier = Modifier.padding(top = 6.dp),
                )
            }
        }
    }
}

/** The cash a cleaner holds: the meter at its true ratio with both ends labelled; no cap, the amount alone. */
@Composable
private fun CashPanel(cash: CashHeld) {
    val locale = appLocale()
    val symbol = CurrencySymbols.forCode(cash.currencyCode)
    val held = OrdersBoardRules.money(cash.amount, symbol, locale)
    val ratio = OrdersBoardRules.cashRatio(cash.amount, cash.floatCap)
    val title = MaterialTheme.typography.titleMedium
    Panel(padded = true) {
        Row {
            Text(text = stringResource(R.string.cash_held_title), style = title, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f).alignByBaseline())
            Text(text = held, style = title.copy(fontFeatureSettings = "tnum"), color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.alignByBaseline())
        }
        if (ratio != null && cash.floatCap != null) {
            val limit = OrdersBoardRules.money(cash.floatCap, symbol, locale)
            val small = MaterialTheme.typography.bodySmall.copy(fontFeatureSettings = "tnum")
            CashMeter(fill = ratio, over = false, track = MaterialTheme.colorScheme.surfaceVariant, modifier = Modifier.padding(top = Spacing.XXS))
            Row {
                Text(text = "0", style = small, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.weight(1f))
                Text(text = stringResource(R.string.cash_held_of_limit, held, limit), style = small, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Text(
                text = OrdersBoardRules.tie(stringResource(R.string.cash_held_hint)),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = Spacing.XS),
            )
        }
    }
}

/** 10 high, a capsule; amber at [fill]; [over] puts an ink tick at [fill] and the rest in the error tone. */
@Composable
private fun CashMeter(fill: Float, over: Boolean, track: Color, modifier: Modifier = Modifier) {
    val amber = MaterialTheme.colorScheme.pendingInk
    val red = MaterialTheme.colorScheme.error
    val tick = MaterialTheme.colorScheme.onSurface
    Canvas(modifier.fillMaxWidth().height(18.dp).clearAndSetSemantics {}) {
        val top = 4.dp.toPx()
        val h = 10.dp.toPx()
        val r = androidx.compose.ui.geometry.CornerRadius(h / 2f)
        val at = size.width * fill.coerceIn(0f, 1f)
        drawRoundRect(track, topLeft = androidx.compose.ui.geometry.Offset(0f, top), size = androidx.compose.ui.geometry.Size(size.width, h), cornerRadius = r)
        if (over) drawRoundRect(red, topLeft = androidx.compose.ui.geometry.Offset(0f, top), size = androidx.compose.ui.geometry.Size(size.width, h), cornerRadius = r)
        drawRoundRect(amber, topLeft = androidx.compose.ui.geometry.Offset(0f, top), size = androidx.compose.ui.geometry.Size(at, h), cornerRadius = r)
        if (over) {
            drawRect(red, topLeft = androidx.compose.ui.geometry.Offset(at, top), size = androidx.compose.ui.geometry.Size((size.width - at - r.x).coerceAtLeast(0f), h))
            drawRect(tick, topLeft = androidx.compose.ui.geometry.Offset(at - 1.dp.toPx(), 0f), size = androidx.compose.ui.geometry.Size(2.dp.toPx(), size.height))
        }
    }
}

@Composable
private fun StaleStrip(loadedAt: Instant?, now: Instant, onRetry: () -> Unit) {
    val locale = appLocale()
    val whenText = loadedAt?.let {
        OrdersBoardRules.staleWhen(it, now, ZoneId.systemDefault(), locale, DateFormat.getBestDateTimePattern(locale, "EEEdMMM"))
    }.orEmpty()
    val ink = MaterialTheme.colorScheme.pendingInk
    val style = MaterialTheme.typography.bodyMedium
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(amberFill(), MaterialTheme.shapes.medium)
            .padding(start = Spacing.M, end = Spacing.XS),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(Icons.Outlined.Refresh, contentDescription = null, tint = ink, modifier = Modifier.size(18.dp))
        Text(
            text = OrdersBoardRules.tie(stringResource(R.string.orders_stale_since, whenText)),
            style = style,
            color = ink,
            modifier = Modifier.weight(1f).padding(horizontal = 10.dp, vertical = Spacing.S).announcePolitely(),
        )
        CleansiaTextLink(text = stringResource(R.string.retry), onClick = onRetry)
    }
}

@Composable
private fun AvailableFooter(pane: PaneState, onLoadMore: () -> Unit) {
    val total = pane.total ?: return
    if (pane.rows.size >= total) return
    Column(
        modifier = Modifier.fillMaxWidth().padding(top = Spacing.XXS),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(Spacing.XXS),
    ) {
        if (pane.pagingFailed) {
            Text(
                text = stringResource(R.string.orders_load_more_failed),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.announcePolitely(),
            )
            CleansiaTextLink(text = stringResource(R.string.retry), onClick = onLoadMore)
        } else {
            Text(
                text = stringResource(R.string.available_showing_of, pane.rows.size, total),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )
            if (pane.isPaging) {
                Box(Modifier.fillMaxWidth().heightIn(min = 48.dp), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator(modifier = Modifier.size(18.dp), color = primaryText(), strokeWidth = 2.dp)
                }
            } else {
                BrandOutlinedButton(text = stringResource(R.string.available_load_more), onClick = onLoadMore)
            }
        }
    }
}

/** Loading at real size: the toolbar's labels at once, two days of flat bars, each row with its pay and chevron column. */
@Composable
private fun AvailableLoading() {
    Column(verticalArrangement = Arrangement.spacedBy(Spacing.S)) {
        Row(Modifier.fillMaxWidth().padding(vertical = Spacing.XXS), verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(170.dp, 16.dp).background(MaterialTheme.colorScheme.surfaceVariant, RoundedCornerShape(6.dp)))
            Spacer(Modifier.weight(1f))
            Text(text = stringResource(R.string.sort_soonest_first), style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        repeat(2) {
            Panel(modifier = Modifier.clearAndSetSemantics {}) {
                Row(Modifier.padding(horizontal = Spacing.M, vertical = Spacing.S), horizontalArrangement = Arrangement.spacedBy(Spacing.S), verticalAlignment = Alignment.CenterVertically) {
                    Box(Modifier.size(TileSize.Header.minSide).background(MaterialTheme.colorScheme.surfaceVariant, MaterialTheme.shapes.small))
                    Column(Modifier.weight(1f)) {
                        Bar(90.dp, 20.dp)
                        Box(Modifier.padding(top = 6.dp).fillMaxWidth().height(10.dp).background(MaterialTheme.colorScheme.surfaceVariant, CircleShape))
                        Spacer(Modifier.height(18.dp))
                    }
                }
                listOf(150.dp to 190.dp, 170.dp to 175.dp).forEach { (area, scope) ->
                    Rule()
                    Row(Modifier.height(IntrinsicSize.Min)) {
                        Column(Modifier.weight(1f).padding(start = Spacing.M, end = Spacing.S, top = Spacing.S, bottom = Spacing.M)) {
                            Row {
                                Bar(116.dp, 22.dp)
                                Spacer(Modifier.weight(1f))
                                Bar(64.dp, 20.dp)
                            }
                            Bar(area, 16.dp, Modifier.padding(top = 6.dp))
                            Bar(scope, 14.dp, Modifier.padding(top = Spacing.XS))
                        }
                        ChevronEdge(showsChevron = false)
                    }
                }
            }
        }
    }
}

/** *Your next jobs · 3 jobs*: her own rows, from the Active call, under a short state. */
private fun LazyListScope.nextJobs(active: PaneState, now: Instant, onOrderClick: (String) -> Unit) {
    val rows = active.rows.sortedWith(compareBy(nullsLast()) { OrdersBoardRules.start(it) })
    if (rows.isEmpty()) return
    item(key = "next-heading") { SectionHeading(stringResource(R.string.orders_your_next_jobs), rows.size) }
    item(key = "next-jobs") {
        Panel {
            rows.forEachIndexed { index, row ->
                if (index > 0) Rule()
                OwnJobRow(row = row, now = now, onClick = { row.id?.let(onOrderClick) }, flags = false)
            }
        }
    }
}

@Composable
private fun ActivePane(
    uiState: OrdersListUiState,
    now: Instant,
    list: LazyListState,
    onOrderClick: (String) -> Unit,
    onNotifyOnTheWay: (String) -> Unit,
    onStart: (String) -> Unit,
    onRetry: () -> Unit,
    onShowAvailable: () -> Unit,
) {
    val rules = OrdersBoardRules
    val zone = ZoneId.systemDefault()
    val pane = uiState.pane(OrdersTab.MyActive)
    val available = uiState.pane(OrdersTab.Available)
    val hero = remember(pane.rows) { rules.heroPick(pane.rows) }
    val later = remember(pane.rows, hero) {
        pane.rows.filter { it.id != hero?.id }.sortedWith(compareBy(nullsLast()) { rules.start(it) })
    }
    val open = remember(available.rows, uiState.reservedRows, now) {
        rules.mergePage(available.rows, uiState.reservedRows.values.toList()).filterNot { rules.isExpired(it, now) }
            .sortedWith(compareBy(nullsLast()) { rules.start(it) })
    }
    val openTotal = rules.availableBadge(available.hasLoaded, available.total, available.rows, now)
    val large = LocalDensity.current.fontScale >= 1.3f
    LazyColumn(
        state = list,
        modifier = Modifier.fillMaxSize(),
        contentPadding = PanePadding,
        verticalArrangement = Arrangement.spacedBy(Spacing.S),
    ) {
        when {
            !pane.hasLoaded && !pane.failed -> item { ActiveLoading() }
            pane.showsError() -> {
                item {
                    StatePanel(
                        title = stringResource(R.string.orders_load_error),
                        body = stringResource(R.string.available_empty_subtitle),
                        mascot = R.drawable.mascot_resting,
                        mascotSize = 112.dp,
                        action = stringResource(R.string.retry),
                        onAction = onRetry,
                    )
                }
                if (available.hasLoaded) {
                    item(key = "week") {
                        WeekPanel(
                            title = stringResource(R.string.active_open_next_7_days),
                            subtitle = null,
                            cells = rules.weekCells(now.atZone(zone).toLocalDate(), open, emptyList(), zone, loaded = available.hasLoaded),
                            openTotal = openTotal,
                            onShowAvailable = onShowAvailable,
                            onCheckAgain = null,
                        )
                    }
                }
                uiState.cashHidden?.let { item(key = "cash-notice") { CashNotice(it) } }
                if (uiState.cashHidden == null) uiState.cashHeld?.let { item(key = "cash") { CashPanel(it) } }
            }
            pane.rows.isEmpty() -> {
                item(key = "stand") { ActiveStand(large) }
                rules.reservedShown(uiState.offers, available.rows + uiState.reservedRows.values, now).firstOrNull()?.let { (offer, row) ->
                    item(key = "teaser") { OfferTeaser(offer = offer, row = row, now = now, onClick = onShowAvailable) }
                }
                item(key = "week") {
                    WeekPanel(
                        title = stringResource(R.string.active_open_next_7_days),
                        subtitle = null,
                        cells = rules.weekCells(now.atZone(zone).toLocalDate(), open, emptyList(), zone, loaded = available.hasLoaded),
                        openTotal = openTotal,
                        onShowAvailable = onShowAvailable,
                        onCheckAgain = onRetry,
                    )
                }
            }
            else -> {
                if (pane.failed) item { StaleStrip(loadedAt = pane.loadedAt, now = now, onRetry = onRetry) }
                hero?.let { row ->
                    item(key = "hero") {
                        ActiveHero(
                            row = row,
                            now = now,
                            progress = uiState.heroProgress,
                            isBusy = uiState.inFlightActionOrderId == row.id,
                            onOpen = { row.id?.let(onOrderClick) },
                            onNotifyOnTheWay = { row.id?.let(onNotifyOnTheWay) },
                            onStart = { row.id?.let(onStart) },
                        )
                    }
                }
                if (later.isNotEmpty()) {
                    item(key = "later-heading") { SectionHeading(stringResource(R.string.day_later), later.size) }
                    item(key = "later") {
                        Panel {
                            later.forEachIndexed { index, row ->
                                if (index > 0) Rule()
                                OwnJobRow(row = row, now = now, onClick = { row.id?.let(onOrderClick) })
                            }
                        }
                    }
                } else {
                    item(key = "week") {
                        WeekPanel(
                            title = stringResource(R.string.active_nothing_else),
                            subtitle = stringResource(R.string.active_open_next_7_days),
                            cells = rules.weekCells(now.atZone(zone).toLocalDate(), open, pane.rows, zone, loaded = available.hasLoaded),
                            openTotal = openTotal,
                            onShowAvailable = onShowAvailable,
                            onCheckAgain = null,
                        )
                    }
                    val own = if (pane.hasLoaded && !pane.failed && pane.complete) pane.rows else emptyList()
                    rules.soonestOpen(open, own, now)?.let { first ->
                        item(key = "soonest-heading") { SectionHeading(stringResource(R.string.active_soonest_open)) }
                        item(key = "soonest") {
                            Panel {
                                AvailableJobRow(
                                    row = first,
                                    now = now,
                                    clash = null,
                                    onOpen = { first.id?.let(onOrderClick) },
                                )
                            }
                        }
                    }
                }
            }
        }
        item { Spacer(Modifier.height(MainBottomNavInset)) }
    }
}

/** No jobs of her own: the answer and what she will find here, the ready mascot standing on the card below. */
@Composable
private fun ActiveStand(large: Boolean) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .then(
                if (large) {
                    Modifier
                } else {
                    Modifier.layout { measurable, constraints ->
                        val placeable = measurable.measure(constraints)
                        layout(placeable.width, placeable.height - StandOverlap.roundToPx()) { placeable.place(0, 0, zIndex = 1f) }
                    }
                },
            ),
        verticalAlignment = Alignment.Bottom,
        horizontalArrangement = Arrangement.spacedBy(Spacing.S),
    ) {
        Column(Modifier.weight(1f).padding(bottom = if (large) 0.dp else Spacing.L)) {
            Text(
                text = OrdersBoardRules.tie(stringResource(R.string.no_active_orders)),
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.semantics { heading() },
            )
            Text(
                text = OrdersBoardRules.tie(stringResource(R.string.active_empty_body)),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = Spacing.XXS),
            )
        }
        if (!large) Image(painter = painterResource(R.drawable.mascot_ready), contentDescription = null, modifier = Modifier.size(156.dp))
    }
}

/** How far the standing figure's feet reach onto the card under it. */
private val StandOverlap = 16.dp + Spacing.S

/** The reserved offer as a link card on an empty Active tab: it opens Available, where the card can be confirmed. */
@Composable
private fun OfferTeaser(offer: PendingOffer, row: OrderListItem, now: Instant, onClick: () -> Unit) {
    val rules = OrdersBoardRules
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val until = reservedUntilLabel(offer.respondByUtc, now.toEpochMilli())
    val then = OrdersBoardRules.tie(stringResource(R.string.available_reserved_then))
    val weekday = rules.start(row)?.let { DateTimeFormatter.ofPattern("EEEE", locale).format(it.atZone(zone)).replaceFirstChar { c -> c.titlecase(locale) } }
    val window = rules.window(rules.start(row), rules.minutes(row), zone, locale)
    val pay = rules.payText(row, locale)
    val scheme = MaterialTheme.colorScheme
    val seeOffer = stringResource(R.string.active_see_offer)
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(MaterialTheme.shapes.medium)
            .background(scheme.surface)
            .border(BorderStroke(1.5.dp, scheme.primary), MaterialTheme.shapes.medium)
            .clickable(onClick = onClick),
    ) {
        Row(Modifier.padding(start = Spacing.M, end = Spacing.M, top = Spacing.M), horizontalArrangement = Arrangement.spacedBy(Spacing.S)) {
            OrdersDateTile(start = rules.start(row), size = TileSize.Header, now = now)
            Column {
                Text(text = OrdersBoardRules.tie(stringResource(R.string.offers_card_title)), style = MaterialTheme.typography.titleMedium, color = scheme.onSurface)
                DotFlow(
                    items = listOf(styled(until, androidx.compose.ui.text.SpanStyle(color = primaryText())), styled(then, androidx.compose.ui.text.SpanStyle(color = scheme.onSurfaceVariant))),
                    style = MaterialTheme.typography.bodyMedium,
                    color = scheme.onSurface,
                    modifier = Modifier.padding(top = 2.dp),
                )
            }
        }
        val runStyle = MaterialTheme.typography.bodyLarge.copy(fontFeatureSettings = "tnum")
        val areaStyle = MaterialTheme.typography.bodyMedium
        val area = rules.areaLine(row)
        val job = @Composable { modifier: Modifier ->
            Column(modifier) {
                DotFlow(
                    items = listOfNotNull(weekday, window).map { AnnotatedString(it) },
                    style = runStyle,
                    color = scheme.onSurface,
                )
                DotFlow(
                    items = listOfNotNull(
                        area?.let { AnnotatedString(it) },
                        rules.bandText(row.homeDistanceBand)?.text()?.let { styled(it, androidx.compose.ui.text.SpanStyle(color = scheme.onSurfaceVariant)) },
                    ),
                    style = areaStyle,
                    color = scheme.onSurface,
                    modifier = Modifier.padding(top = 2.dp),
                )
            }
        }
        val spec = TileSpec(pay = pay, action = seeOffer)
        val tileWidth = payTileWidth(listOf(spec))
        val measurer = rememberTextMeasurer()
        val density = LocalDensity.current
        BoxWithConstraints(Modifier.padding(start = Spacing.M, end = Spacing.M, top = Spacing.S, bottom = Spacing.M)) {
            val runs = listOfNotNull(weekday?.let { it to runStyle }, window?.let { it to runStyle }, area?.let { it to areaStyle })
                .map { (text, style) -> measurer.measure(text, style, maxLines = 1, softWrap = false).size.width }
            val column = with(density) { (maxWidth - tileWidth - Spacing.S).roundToPx() }
            if (rules.offerStacks(runs, column, density.fontScale)) {
                Column(verticalArrangement = Arrangement.spacedBy(Spacing.S)) {
                    job(Modifier.fillMaxWidth())
                    PayTile(spec = spec, description = seeOffer, onClick = onClick, modifier = Modifier.fillMaxWidth(), wide = true)
                }
            } else {
                Row(horizontalArrangement = Arrangement.spacedBy(Spacing.S)) {
                    job(Modifier.weight(1f))
                    PayTile(spec = spec, description = seeOffer, onClick = onClick, modifier = Modifier.width(tileWidth))
                }
            }
        }
    }
}

/** *Open jobs, next 7 days*: the week strip from today, *See N open jobs* (the Available badge, none without one) and, empty, *Check again*. */
@Composable
internal fun WeekPanel(
    title: String,
    subtitle: String?,
    cells: List<WeekCell>,
    openTotal: Int?,
    onShowAvailable: () -> Unit,
    onCheckAgain: (() -> Unit)?,
) {
    Panel(padded = true) {
        Text(text = OrdersBoardRules.tie(title), style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.semantics { heading() })
        subtitle?.let { Text(text = OrdersBoardRules.tie(it), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 2.dp)) }
        WeekStrip(cells = cells, modifier = Modifier.padding(top = Spacing.S))
        if (cells.any { it.mine }) {
            val caption = MaterialTheme.typography.bodySmall
            Row(Modifier.padding(top = Spacing.XS)) {
                Box(
                    xBandAligned(caption)
                        .size(10.dp)
                        .background(tonalFill(), RoundedCornerShape(3.dp))
                        .border(1.5.dp, MaterialTheme.colorScheme.primary, RoundedCornerShape(3.dp)),
                )
                Text(
                    text = stringResource(R.string.active_week_legend_your_job),
                    style = caption,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(start = 6.dp).alignBy(FirstBaseline).clearAndSetSemantics {},
                )
            }
        }
        if (openTotal != null) {
            BrandOutlinedButton(
                text = OrdersBoardRules.tie(pluralStringResource(R.plurals.active_see_open, openTotal, openTotal)),
                onClick = onShowAvailable,
                maxLines = 2,
                modifier = Modifier.padding(top = Spacing.S),
            )
        }
        if (onCheckAgain != null) {
            Box(Modifier.fillMaxWidth().padding(top = Spacing.XXS), contentAlignment = Alignment.Center) {
                CleansiaTextLink(text = stringResource(R.string.orders_check_again), onClick = onCheckAgain, leadingIcon = Icons.Outlined.Refresh)
            }
        }
    }
}

/**
 * Seven days from today: the weekday heading each column, the day a square card holding its number, the open count
 * under it once Available has answered (a real 0 in grey); her days carry the selected stroke.
 */
@Composable
internal fun WeekStrip(cells: List<WeekCell>, modifier: Modifier = Modifier) {
    val locale = appLocale()
    val scheme = MaterialTheme.colorScheme
    val caption = MaterialTheme.typography.bodySmall.copy(fontFeatureSettings = "tnum")
    Row(modifier = modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        cells.forEach { cell ->
            val said = OrdersBoardRules.weekCellLabel(cell, DateTimeFormatter.ofPattern("EEEE d MMMM", locale).format(cell.day)).text()
            val open = cell.open ?: 0
            val shape = MaterialTheme.shapes.small
            Column(
                modifier = Modifier.weight(1f).clearAndSetSemantics { contentDescription = said },
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Text(
                    text = DateTimeFormatter.ofPattern("EEE", locale).format(cell.day).trimEnd('.'),
                    style = caption,
                    color = scheme.onSurfaceVariant,
                    maxLines = 1,
                    modifier = Modifier.padding(bottom = Spacing.XXS),
                )
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .aspectRatio(1f)
                        .background(if (cell.mine || open > 0) tonalFill() else scheme.surfaceVariant, shape)
                        .then(if (cell.mine) Modifier.border(1.5.dp, scheme.primary, shape) else Modifier),
                    contentAlignment = Alignment.Center,
                ) {
                    Text(
                        text = cell.day.dayOfMonth.toString(),
                        style = MaterialTheme.typography.titleMedium.copy(fontFeatureSettings = "tnum"),
                        color = if (cell.mine || open > 0) scheme.onSurface else scheme.onSurfaceVariant,
                    )
                }
                Text(
                    text = cell.open?.toString().orEmpty(),
                    style = caption,
                    color = if (open > 0) primaryText() else scheme.onSurfaceVariant,
                    modifier = Modifier.padding(top = Spacing.XXS),
                )
            }
        }
    }
}

/** Loading at real size: the hero's head, day line, scope and slide as flat bars, then the *Later* rows at their pitch. */
@Composable
private fun ActiveLoading() {
    Column(verticalArrangement = Arrangement.spacedBy(Spacing.S), modifier = Modifier.clearAndSetSemantics {}) {
        Panel(padded = true) {
            Bar(170.dp, 22.dp)
            Bar(190.dp, 20.dp, Modifier.padding(top = 2.dp))
            Bar(220.dp, 22.dp, Modifier.padding(top = Spacing.S))
            Box(Modifier.padding(top = Spacing.XS).fillMaxWidth().height(4.dp).background(MaterialTheme.colorScheme.surfaceVariant, RoundedCornerShape(2.dp)))
            Bar(150.dp, 24.dp, Modifier.padding(top = Spacing.M))
            Box(Modifier.padding(top = Spacing.M).fillMaxWidth().height(10.dp).background(MaterialTheme.colorScheme.surfaceVariant, CircleShape))
            Bar(220.dp, 20.dp, Modifier.padding(top = Spacing.L))
            Box(Modifier.padding(top = Spacing.S).fillMaxWidth().height(56.dp).background(MaterialTheme.colorScheme.surfaceVariant, CircleShape))
        }
        Bar(90.dp, 20.dp, Modifier.padding(top = Spacing.XS))
        Panel {
            listOf(150.dp, 120.dp, 170.dp).forEachIndexed { index, street ->
                if (index > 0) Rule()
                Row(Modifier.padding(horizontal = Spacing.M, vertical = Spacing.S), horizontalArrangement = Arrangement.spacedBy(Spacing.S)) {
                    Box(Modifier.size(TileSize.Row.minSide).background(MaterialTheme.colorScheme.surfaceVariant, MaterialTheme.shapes.small))
                    Column(Modifier.weight(1f)) {
                        Bar(118.dp, 24.dp)
                        Bar(street, 20.dp, Modifier.padding(top = 2.dp))
                    }
                    Bar(56.dp, 22.dp)
                }
            }
        }
    }
}

@Composable
private fun HistoryPane(
    uiState: OrdersListUiState,
    now: Instant,
    list: LazyListState,
    onOrderClick: (String) -> Unit,
    onPeriodSelected: (CompletedPeriod) -> Unit,
    onRetry: () -> Unit,
    onOpenEarnings: () -> Unit,
) {
    val pane = uiState.pane(OrdersTab.MyCompleted)
    // A zero total still names the cleaner's currency when any of their rows does.
    val fallbackSymbol = (uiState.pane(OrdersTab.MyActive).rows + uiState.pane(OrdersTab.Available).rows)
        .firstNotNullOfOrNull { it.currency?.symbol?.takeIf(String::isNotBlank) }
    LazyColumn(
        state = list,
        modifier = Modifier.fillMaxSize(),
        contentPadding = PanePadding,
        verticalArrangement = Arrangement.spacedBy(Spacing.S),
    ) {
        if (pane.failed && pane.rows.isNotEmpty()) item(key = "stale") { StaleStrip(loadedAt = pane.loadedAt, now = now, onRetry = onRetry) }
        item(key = "history") {
            HistoryPanel(
                pane = pane,
                reference = uiState.historyReference,
                period = uiState.completedPeriod,
                now = now,
                fallbackSymbol = fallbackSymbol,
                onPeriodSelected = onPeriodSelected,
                onRetry = onRetry,
                onOpenEarnings = onOpenEarnings,
                onOrderClick = onOrderClick,
            )
        }
        if (pane.showsError()) {
            nextJobs(uiState.pane(OrdersTab.MyActive), now, onOrderClick)
            uiState.cashHeld?.let { item { CashPanel(it) } }
        }
        item { Spacer(Modifier.height(MainBottomNavInset)) }
    }
}

/** History as one card: the period chips, the total, the month, the payouts link, then the ledger by day. */
@Composable
private fun HistoryPanel(
    pane: PaneState,
    reference: PaneState?,
    period: CompletedPeriod,
    now: Instant,
    fallbackSymbol: String?,
    onPeriodSelected: (CompletedPeriod) -> Unit,
    onRetry: () -> Unit,
    onOpenEarnings: () -> Unit,
    onOrderClick: (String) -> Unit,
) {
    val rules = OrdersBoardRules
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val today = now.atZone(zone).toLocalDate()
    val names = periodNames(locale, today)
    val chevron = Icons.AutoMirrored.Outlined.KeyboardArrowRight
    val payouts: @Composable () -> Unit = {
        CleansiaTextLink(
            text = stringResource(R.string.history_payouts_link, stringResource(R.string.earnings_title)),
            onClick = onOpenEarnings,
            trailingIcon = chevron,
            maxLines = 2,
            modifier = Modifier.padding(top = Spacing.XXS).offsetStart(),
        )
    }
    Panel(padded = true) {
        PeriodChips(names = names, current = period, onSelected = onPeriodSelected)
        if (pane.showsError()) {
            Row(Modifier.padding(vertical = Spacing.S), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(Spacing.S)) {
                Column(Modifier.weight(1f)) {
                    val title = MaterialTheme.typography.titleMedium
                    Row {
                        Icon(Icons.Outlined.ErrorOutline, contentDescription = null, tint = MaterialTheme.colorScheme.error, modifier = capBandAligned(title).padding(end = 8.dp).size(18.dp))
                        Text(text = OrdersBoardRules.tie(stringResource(R.string.orders_load_error)), style = title, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.alignBy(FirstBaseline).announcePolitely())
                    }
                    Text(text = OrdersBoardRules.tie(stringResource(R.string.available_empty_subtitle)), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 2.dp))
                    BrandOutlinedButton(text = stringResource(R.string.retry), onClick = onRetry, leadingIcon = Icons.Outlined.Refresh, modifier = Modifier.padding(top = Spacing.S).width(IntrinsicSize.Max))
                }
                Image(painter = painterResource(R.drawable.mascot_resting), contentDescription = null, modifier = Modifier.size(120.dp))
            }
            payouts()
            return@Panel
        }
        val (from, to) = periodDays(period, today)
        val gridEnd = rules.historyGridEnd(to, today)
        val loading = !pane.hasLoaded
        Text(
            text = OrdersBoardRules.tie(stringResource(R.string.history_rewards_title)),
            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
            color = primaryText(),
            modifier = Modifier.padding(top = Spacing.M).semantics { heading() },
        )
        if (loading) {
            Bar(150.dp, 30.dp, Modifier.padding(vertical = 2.dp))
            Bar(200.dp, 16.dp)
        } else {
            HistoryTotal(rows = pane.rows, total = pane.total, fallbackSymbol = fallbackSymbol, periodName = names.getValue(period))
        }
        MonthGrid(from = from, to = gridEnd, rows = if (loading) emptyList() else pane.rows, today = today, modifier = Modifier.padding(top = Spacing.M))
        payouts()
        if (loading) {
            listOf(2 to 150.dp, 1 to 120.dp, 1 to 170.dp).forEach { (jobs, width) ->
                Rule(Modifier.offsetHorizontal())
                Row(Modifier.padding(vertical = Spacing.S).clearAndSetSemantics {}, horizontalArrangement = Arrangement.spacedBy(Spacing.S)) {
                    Box(Modifier.size(TileSize.Row.minSide).background(MaterialTheme.colorScheme.surfaceVariant, MaterialTheme.shapes.small))
                    Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(Spacing.S)) {
                        repeat(jobs) {
                            Column {
                                Row {
                                    Bar(118.dp, 22.dp)
                                    Spacer(Modifier.weight(1f))
                                    Bar(56.dp, 22.dp)
                                }
                                Bar(width, 14.dp, Modifier.padding(top = 6.dp))
                            }
                        }
                    }
                }
            }
            return@Panel
        }
        if (pane.rows.isEmpty()) {
            Rule(Modifier.offsetHorizontal())
            Row(Modifier.padding(vertical = Spacing.S), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(Spacing.S)) {
                Column(Modifier.weight(1f)) {
                    Text(
                        text = OrdersBoardRules.tie(stringResource(if (period == CompletedPeriod.ThisWeek) R.string.history_empty_week else R.string.history_empty_month)),
                        style = MaterialTheme.typography.titleMedium,
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    Text(text = OrdersBoardRules.tie(stringResource(R.string.history_empty_body)), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 2.dp))
                }
                Image(painter = painterResource(R.drawable.mascot_resting), contentDescription = null, modifier = Modifier.size(88.dp))
            }
            reference?.takeIf { period == CompletedPeriod.ThisMonth }?.let { ref ->
                val lastName = names.getValue(CompletedPeriod.LastMonth)
                Rule(Modifier.offsetHorizontal())
                Column(Modifier.padding(top = Spacing.S)) {
                    Text(text = stringResource(R.string.history_last_month), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    val totals = rules.historyTotals(ref.rows, ref.total)
                    Row {
                        Text(text = lastName, style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f).alignByBaseline())
                        Text(
                            text = rules.historyTotalText(ref.rows, ref.total, fallbackSymbol, locale),
                            style = MaterialTheme.typography.titleMedium.copy(fontFeatureSettings = "tnum"),
                            color = MaterialTheme.colorScheme.onSurface,
                            softWrap = false,
                            modifier = Modifier.alignByBaseline(),
                        )
                    }
                    DotFlow(
                        items = summaryItems(totals.jobs, totals.hours).map { AnnotatedString(it) },
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(top = 2.dp),
                    )
                    val (lastFrom, lastTo) = periodDays(CompletedPeriod.LastMonth, today)
                    MonthGrid(from = lastFrom, to = lastTo, rows = ref.rows, today = today, modifier = Modifier.padding(top = 10.dp))
                    val lower = if (locale.language == "en") lastName else lastName.lowercase(locale)
                    CleansiaTextLink(
                        text = stringResource(R.string.history_show_period, lower),
                        onClick = { onPeriodSelected(CompletedPeriod.LastMonth) },
                        trailingIcon = chevron,
                        modifier = Modifier.padding(top = Spacing.XXS).offsetStart(),
                    )
                }
            }
            return@Panel
        }
        rules.dayGroups(pane.rows, zone).forEach { (_, rows) ->
            Rule(Modifier.offsetHorizontal())
            LedgerDay(rows = rows, now = now, onOrderClick = onOrderClick)
        }
    }
}

/** A rule inside a padded card reaches its edges. */
internal fun Modifier.offsetHorizontal(): Modifier = layout { measurable, constraints ->
    val extra = Spacing.M.roundToPx() * 2
    val placeable = measurable.measure(constraints.copy(minWidth = constraints.maxWidth + extra, maxWidth = constraints.maxWidth + extra))
    layout(constraints.maxWidth, placeable.height) { placeable.place(-Spacing.M.roundToPx(), 0) }
}

/** A text link at the start of a column: its own padding hangs outside, so its words start on the column's edge. */
private fun Modifier.offsetStart(): Modifier = offset(x = -LinkPadding)

private val LinkPadding = 4.dp

@Composable
private fun summaryItems(jobs: Int, hours: Int): List<String> {
    val jobsText = pluralStringResource(R.plurals.orders_job_count, jobs, jobs)
    if (jobs == 0) return listOf(jobsText)
    return stringResource(R.string.history_summary, jobsText, stringResource(R.string.orders_duration_h, hours)).split(OrdersBoardRules.SEP, " · ")
}

/** The period's one money hero, one string, and its jobs and hours under it. */
@Composable
private fun HistoryTotal(rows: List<OrderListItem>, total: Int?, fallbackSymbol: String?, periodName: String) {
    val locale = appLocale()
    val totals = OrdersBoardRules.historyTotals(rows, total)
    val earnings = OrdersBoardRules.historyTotalText(rows, total, fallbackSymbol, locale)
    val eyebrow = stringResource(R.string.history_rewards_title)
    Text(
        text = earnings,
        style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold, fontFeatureSettings = "tnum"),
        color = MaterialTheme.colorScheme.onSurface,
        softWrap = false,
        modifier = Modifier.semantics { contentDescription = "$eyebrow, $periodName: $earnings" },
    )
    DotFlow(
        items = summaryItems(totals.jobs, totals.hours).map { AnnotatedString(it) },
        style = MaterialTheme.typography.bodyMedium,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier.padding(top = 2.dp),
    )
}

/** This week (Monday-first), the current month, the previous month, as dates. */
private fun periodDays(period: CompletedPeriod, today: LocalDate): Pair<LocalDate, LocalDate> = when (period) {
    CompletedPeriod.ThisWeek -> today.with(java.time.temporal.TemporalAdjusters.previousOrSame(java.time.DayOfWeek.MONDAY)).let { it to it.plusDays(6) }
    CompletedPeriod.ThisMonth -> today.withDayOfMonth(1).let { it to it.plusMonths(1).minusDays(1) }
    CompletedPeriod.LastMonth -> today.withDayOfMonth(1).minusMonths(1).let { it to it.plusMonths(1).minusDays(1) }
}

/**
 * Monday-first weeks of square days: a worked day on the app's tint with one dot per job hanging under its digit, so the
 * digits of a week share one line; today with the selected stroke; days outside the period quiet.
 */
@Composable
private fun MonthGrid(from: LocalDate, to: LocalDate, rows: List<OrderListItem>, today: LocalDate, modifier: Modifier = Modifier) {
    val zone = ZoneId.systemDefault()
    val locale = appLocale()
    val scheme = MaterialTheme.colorScheme
    val quiet = if (isDarkScheme()) Slate400 else Slate500
    val perDay = rows.groupingBy { OrdersBoardRules.day(it, zone) }.eachCount()
    val days = OrdersBoardRules.historyGrid(from, to)
    val weekdays = remember(locale) {
        val names = android.icu.text.DateFormatSymbols.getInstance(locale)
            .getWeekdays(android.icu.text.DateFormatSymbols.STANDALONE, android.icu.text.DateFormatSymbols.SHORT)
        listOf(2, 3, 4, 5, 6, 7, 1).map { names[it].replaceFirstChar { c -> c.titlecase(locale) } }
    }
    val shape = MaterialTheme.shapes.small
    Column(modifier.clearAndSetSemantics {}, verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
            weekdays.forEach {
                Text(text = it, style = MaterialTheme.typography.bodySmall, color = scheme.onSurfaceVariant, textAlign = TextAlign.Center, modifier = Modifier.weight(1f).padding(bottom = 2.dp))
            }
        }
        days.chunked(7).forEach { week ->
            Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                week.forEach { day ->
                    val count = perDay[day] ?: 0
                    val inPeriod = day in from..to
                    val future = day > today
                    Box(
                        modifier = Modifier
                            .weight(1f)
                            .aspectRatio(1f)
                            .background(if (count > 0) tonalFill() else Color.Transparent, shape)
                            .then(if (day == today) Modifier.border(1.5.dp, scheme.primary, shape) else Modifier),
                        contentAlignment = Alignment.Center,
                    ) {
                        Text(
                            text = day.dayOfMonth.toString(),
                            style = MaterialTheme.typography.bodyMedium.copy(fontFeatureSettings = "tnum"),
                            color = if (!inPeriod || future) quiet else scheme.onSurface,
                        )
                        if (count > 0) {
                            Row(horizontalArrangement = Arrangement.spacedBy(3.dp), modifier = Modifier.align(Alignment.BottomCenter).padding(bottom = 4.dp)) {
                                repeat(count) { Box(Modifier.size(5.dp).background(scheme.primary, CircleShape)) }
                            }
                        }
                    }
                }
            }
        }
    }
}

/** One closed day: the grey leaf, its jobs stacked (window ‖ pay, area · service, what happened to the cash); at large text the pay goes last. */
@Composable
private fun LedgerDay(rows: List<OrderListItem>, now: Instant, onOrderClick: (String) -> Unit) {
    val rules = OrdersBoardRules
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val large = LocalDensity.current.fontScale >= 1.3f
    Row(Modifier.padding(vertical = Spacing.S), horizontalArrangement = Arrangement.spacedBy(Spacing.S)) {
        OrdersDateTile(start = rules.start(rows.first()), size = TileSize.Row, now = now, closed = true)
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(Spacing.S)) {
            rows.forEach { row ->
                val window = rules.window(rules.start(row), rules.minutes(row), zone, locale)
                val pay = rules.payText(row, locale)
                val lines = listOfNotNull(rules.areaLine(row), rules.scopeLead(row, locale.language), rules.heroTeam(row)?.text())
                val cash = rules.historyCash(row, locale).map { it.text() }
                val sentence = spokenSentence(
                    listOf(rules.start(row)?.let { dayAndDate(it, now) }, window),
                    lines,
                    listOf(pay?.let { "${stringResource(R.string.you_earn)} $it" }),
                    cash,
                )
                Column(
                    Modifier
                        .fillMaxWidth()
                        .clickable { row.id?.let(onOrderClick) }
                        .clearAndSetSemantics { contentDescription = sentence },
                ) {
                    val windowStyle = MaterialTheme.typography.bodyLarge.copy(fontFeatureSettings = "tnum")
                    val payStyle = MaterialTheme.typography.titleMedium.copy(fontFeatureSettings = "tnum")
                    val shownWindow = window?.let(rules::holdDash) ?: "—"
                    if (large) {
                        Text(text = shownWindow, style = windowStyle, color = MaterialTheme.colorScheme.onSurface)
                    } else {
                        Row {
                            Text(text = shownWindow, style = windowStyle, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f).alignByBaseline())
                            pay?.let { Text(text = it, style = payStyle, color = MaterialTheme.colorScheme.onSurface, softWrap = false, modifier = Modifier.padding(start = Spacing.XS).alignByBaseline()) }
                        }
                    }
                    DotFlow(items = lines.map { AnnotatedString(it) }, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 2.dp))
                    when (cash.size) {
                        1 -> FlagFlow(Modifier.padding(top = Spacing.XS)) { OrderFlag(cash.single(), FlagTone.Warn, Icons.Outlined.Payments) }
                        2 -> FlagFlow(Modifier.padding(top = Spacing.XS)) {
                            OrderFlagShell(FlagTone.Neutral, Icons.Outlined.Payments) { style, ink ->
                                Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                                    Text(text = cash[0], style = style, color = ink)
                                    Text(text = cash[1].replaceFirstChar { it.titlecase(locale) }, style = style, color = ink)
                                }
                            }
                        }
                    }
                    if (large) pay?.let { Text(text = it, style = payStyle, color = MaterialTheme.colorScheme.onSurface, softWrap = false, modifier = Modifier.padding(top = 6.dp)) }
                }
            }
        }
    }
}

/** This week, then the current and previous months by their standalone names ("Listopad", "Листопад"). */
@Composable
private fun periodNames(locale: java.util.Locale, today: LocalDate): Map<CompletedPeriod, String> {
    val month = { date: LocalDate -> DateTimeFormatter.ofPattern("LLLL", locale).format(date).replaceFirstChar { it.titlecase(locale) } }
    return mapOf(
        CompletedPeriod.ThisWeek to stringResource(R.string.period_this_week),
        CompletedPeriod.ThisMonth to month(today),
        CompletedPeriod.LastMonth to month(today.minusMonths(1)),
    )
}

/** The period: the app's chips in a row, each a tab that says whether it is the chosen one. */
@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun PeriodChips(names: Map<CompletedPeriod, String>, current: CompletedPeriod, onSelected: (CompletedPeriod) -> Unit) {
    FlowRow(
        modifier = Modifier.fillMaxWidth().selectableGroup(),
        horizontalArrangement = Arrangement.spacedBy(Spacing.XS),
        verticalArrangement = Arrangement.spacedBy(Spacing.XS),
    ) {
        CompletedPeriod.entries.forEach { period ->
            val chosen = period == current
            CleansiaChip(
                label = names.getValue(period),
                isSelected = chosen,
                onClick = { onSelected(period) },
                role = Role.Tab,
                modifier = Modifier.semantics { selected = chosen },
            )
        }
    }
}
