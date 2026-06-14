package cz.cleansia.partner.features.dashboard.screens

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowForward
import androidx.compose.material.icons.automirrored.outlined.Article
import androidx.compose.material.icons.automirrored.outlined.HelpOutline
import androidx.compose.material.icons.automirrored.outlined.TrendingDown
import androidx.compose.material.icons.automirrored.outlined.TrendingUp
import androidx.compose.material.icons.outlined.AccessTime
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.History
import androidx.compose.material.icons.outlined.NotificationsNone
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material.icons.outlined.WorkOutline
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.material3.pulltorefresh.rememberPullToRefreshState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.AvailableJobsPreviewResponse
import cz.cleansia.partner.api.model.DashboardStatsDto
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.features.dashboard.viewmodels.DashboardUiState
import cz.cleansia.partner.features.dashboard.viewmodels.DashboardViewModel
import cz.cleansia.partner.features.main.MainBottomNavInset
import cz.cleansia.partner.features.orders.components.toOrderStatus
import cz.cleansia.partner.ui.theme.BrandGradients
import cz.cleansia.partner.ui.theme.asList
import java.time.Duration
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle
import java.time.temporal.ChronoUnit
import java.util.Locale

/**
 * Cleaner-first dashboard composition. Designed top-to-bottom around the
 * four questions a cleaner has when they open the app:
 *
 *  1. CompactGreetingBar  — name + date + "free today / N jobs today" state
 *                           line. Tells them their state without making them
 *                           scan a stats grid.
 *  2. TodayHeroCard       — single hero that adapts to state:
 *                           a) active next-job → countdown + address + CTA
 *                           b) free + jobs available → gradient CTA with €X
 *                              potential earnings
 *                           c) free + nothing available → soft empty state
 *  3. EarningsSplitRow    — Today / This week side-by-side. Shows the cleaner
 *                           what they're making right now.
 *  4. PayPeriodCard       — current pay period with progress bar + payout
 *                           date. Answers "when do I get paid?".
 *  5. LastMonthCard       — earnings + jobs + rating, three columns. Last
 *                           month as social proof for self when today is zero.
 *  6. QuickActionsGrid    — 4 tertiary shortcuts (Availability / Pay history
 *                           / Documents / Help) when none of the above
 *                           gives them what they need.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardScreen(
    onOrderClick: (String) -> Unit,
    onOpenOrders: () -> Unit,
    onOpenEarnings: () -> Unit,
    onOpenProfile: () -> Unit,
    onOpenDocuments: () -> Unit,
    onOpenNotifications: () -> Unit,
    viewModel: DashboardViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsState()
    val firstName by viewModel.firstName.collectAsState()
    val unreadNotifications by viewModel.unreadNotifications.collectAsState()
    val statusBarTop = WindowInsets.statusBars.asPaddingValues().calculateTopPadding()

    // Silent-stale on resume. The VM routes through the staleness check
    // internally and either silently background-refreshes (cache > 60s)
    // or no-ops (warm cache from a recent tab swipe). Pull-to-refresh
    // still forces a network call via viewModel.refresh().
    val lifecycleOwner = LocalLifecycleOwner.current
    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) {
                viewModel.onResume()
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    val loaded = uiState as? DashboardUiState.Loaded
    val upcoming = loaded?.upcoming.orEmpty()
    val stats = loaded?.stats
    val availableJobsPreview = loaded?.availableJobsPreview
    val nextJob = remember(upcoming) { pickNextJob(upcoming) }
    val todaysJobs = remember(upcoming) { filterTodaysJobs(upcoming) }

    // Genuine first-ever paint — owns the screen with the centered
    // CircularProgressIndicator. After that first paint, the data renders
    // and background refreshes never re-trigger this branch (stats stays
    // non-null even while a silent refresh swaps numbers under the user).
    val isInitialLoading = uiState is DashboardUiState.Loading

    val pullState = rememberPullToRefreshState()
    PullToRefreshBox(
        // Bind ONLY to user-initiated pulls. Background refreshes
        // (init / ON_RESUME / post-mutation) must render silently — the
        // chunky suds indicator is reserved for the user's own gesture,
        // which is what the silent-stale contract demands. The old
        // single-flag binding leaked auto-refreshes into this indicator.
        isRefreshing = uiState.isUserRefreshing,
        onRefresh = { viewModel.refresh() },
        state = pullState,
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
        // Custom suds-swirl indicator (rotates naturally — on-brand
        // for cleaning, replaces the off-theme claw the dashboard
        // used to ship with). Default M3 indicator anchors at y=0
        // of the box, which sits under the status bar on edge-to-
        // edge layouts and renders the spinner half-clipped — push
        // ours down by the status-bar inset + a small gap.
        indicator = {
            cz.cleansia.core.ui.components.SudsRefreshIndicator(
                state = pullState,
                isRefreshing = uiState.isUserRefreshing,
                modifier = Modifier
                    .align(Alignment.TopCenter)
                    .padding(top = statusBarTop + 8.dp),
            )
        },
    ) {
    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
        contentPadding = PaddingValues(bottom = MainBottomNavInset),
        verticalArrangement = Arrangement.spacedBy(Spacing.M),
    ) {
        item {
            CompactGreetingBar(
                firstName = firstName,
                todaysJobsCount = todaysJobs.size,
                statusBarTop = statusBarTop,
                unreadCount = unreadNotifications,
                onNotificationClick = onOpenNotifications,
            )
        }

        if (isInitialLoading) {
            item {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(Spacing.XL),
                    contentAlignment = Alignment.Center,
                ) {
                    CircularProgressIndicator()
                }
            }
        } else {
            item {
                TodayHeroCard(
                    nextJob = nextJob,
                    preview = availableJobsPreview,
                    currencyCode = stats?.currencyCode,
                    onOpenOrders = onOpenOrders,
                    onOpenOrderDetail = { id -> onOrderClick(id) },
                )
            }

            item {
                EarningsSplitRow(stats = stats, onClick = onOpenEarnings)
            }

            stats?.takeIf { it.currentPayPeriodStart != null }?.let { periodStats ->
                item {
                    PayPeriodCard(stats = periodStats, onClick = onOpenEarnings)
                }
            }

            item {
                LastMonthCard(stats = stats)
            }

            item {
                QuickActionsGrid(
                    onProfile = onOpenProfile,
                    onPayHistory = onOpenEarnings,
                    onDocuments = onOpenDocuments,
                    onHelp = { /* Phase 9: help screen */ },
                )
            }
        }
    }
    }
}

/* ─── 1. Greeting bar ─── */

@Composable
private fun CompactGreetingBar(
    firstName: String?,
    todaysJobsCount: Int,
    statusBarTop: androidx.compose.ui.unit.Dp,
    unreadCount: Int,
    onNotificationClick: () -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = statusBarTop)
            .padding(horizontal = Spacing.M, vertical = Spacing.S),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        // Small waving mascot anchors the greeting — gives the
        // dashboard a friendly face on every paint instead of just a
        // bell. Sized to match the avatar/header rhythm without
        // crowding the greeting text.
        Image(
            painter = painterResource(R.drawable.mascot_waving),
            contentDescription = null,
            modifier = Modifier.size(40.dp),
        )
        Spacer(Modifier.width(Spacing.S))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = greeting(firstName),
                style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onBackground,
            )
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = LocalDate.now().format(
                        DateTimeFormatter.ofPattern("EEEE, d MMM", Locale.getDefault())
                    ),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Text(
                    text = " · ",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Text(
                    text = todaysStateLine(todaysJobsCount),
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.primary,
                )
            }
        }
        Box {
            IconButton(
                onClick = onNotificationClick,
                modifier = Modifier
                    .size(40.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.surface),
            ) {
                Icon(
                    imageVector = Icons.Outlined.NotificationsNone,
                    contentDescription = stringResource(R.string.notifications),
                    tint = MaterialTheme.colorScheme.onSurface,
                )
            }
            // Unread dot — presence-only (no count) keeps the bell uncluttered;
            // the feed itself shows what's new. Bordered with the background so
            // it reads as a floating pip over the bell.
            if (unreadCount > 0) {
                Box(
                    modifier = Modifier
                        .align(Alignment.TopEnd)
                        .padding(top = 6.dp, end = 6.dp)
                        .size(10.dp)
                        .clip(CircleShape)
                        .background(MaterialTheme.colorScheme.background)
                        .padding(1.dp)
                        .clip(CircleShape)
                        .background(MaterialTheme.colorScheme.error),
                )
            }
        }
    }
}

@Composable
private fun todaysStateLine(count: Int): String = when (count) {
    0 -> stringResource(R.string.dash_state_free_today)
    1 -> stringResource(R.string.dash_state_one_today)
    else -> stringResource(R.string.dash_state_many_today, count)
}

/* ─── 2. Hero: today ─── */

@Composable
private fun TodayHeroCard(
    nextJob: OrderListItem?,
    preview: AvailableJobsPreviewResponse?,
    currencyCode: String?,
    onOpenOrders: () -> Unit,
    onOpenOrderDetail: (String) -> Unit,
) {
    when {
        nextJob != null -> NextJobHero(nextJob, onClick = { nextJob.id?.let(onOpenOrderDetail) })
        (preview?.totalAvailableCount ?: 0) > 0 -> AvailableWorkHero(
            preview = preview!!,
            currencyCode = currencyCode,
            onClick = onOpenOrders,
        )
        else -> EmptyHero(onClick = onOpenOrders)
    }
}

@Composable
private fun NextJobHero(order: OrderListItem, onClick: () -> Unit) {
    val status = order.orderStatus.toOrderStatus()
    val (label, _) = nextJobLabelAndCta(status)
    val whenText = remember(order.cleaningDateTime) { nextJobWhenLine(order.cleaningDateTime) }
    val whereText = remember(order.customerName, order.customerAddress) {
        nextJobWhereLine(order.customerName, order.customerAddress)
    }

    // Same layout as AvailableWorkHero: mascot — label / title /
    // subtitle column — trailing chevron. Larger padding + larger
    // mascot than the dashboard's secondary cards so the next-job
    // card reads as the page's hero.
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = Spacing.M)
            .clip(RoundedCornerShape(20.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(20.dp),
            )
            .clickable { onClick() }
            .padding(Spacing.M + 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Image(
            painter = painterResource(R.drawable.mascot_cleaning),
            contentDescription = null,
            modifier = Modifier.size(72.dp),
        )
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = label,
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = whenText,
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            if (whereText != null) {
                Spacer(Modifier.height(2.dp))
                Text(
                    text = whereText,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis,
                )
            }
        }
        Spacer(Modifier.width(8.dp))
        Icon(
            imageVector = Icons.AutoMirrored.Outlined.ArrowForward,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(20.dp),
        )
    }
}

/**
 * Card title: relative time when the job is soon ("In 35m", "Tomorrow
 * 14:00"), absolute date+time otherwise. Drops the redundant year for
 * any time inside this calendar year.
 */
private fun nextJobWhenLine(iso: String?): String {
    if (iso.isNullOrBlank()) return "—"
    val instant = runCatching { Instant.parse(iso) }.getOrNull() ?: return iso
    val zone = ZoneId.systemDefault()
    val local = instant.atZone(zone)
    val time = local.toLocalTime().format(DateTimeFormatter.ofPattern("HH:mm", Locale.getDefault()))
    val now = Instant.now()
    val duration = Duration.between(now, instant)
    if (!duration.isNegative) {
        val totalMinutes = duration.toMinutes()
        val hours = duration.toHours()
        when {
            totalMinutes < 60 -> return String.format(Locale.getDefault(), "In %d min", totalMinutes)
            hours < 24 -> {
                val today = LocalDate.now(zone)
                if (local.toLocalDate() == today) return "Today $time"
                return String.format(Locale.getDefault(), "In %dh %02dm", hours, totalMinutes % 60)
            }
        }
        val days = duration.toDays()
        if (days == 1L) return "Tomorrow $time"
    }
    val sameYear = local.year == LocalDate.now(zone).year
    val datePattern = if (sameYear) "EEE d MMM" else "d MMM yyyy"
    val day = local.format(DateTimeFormatter.ofPattern(datePattern, Locale.getDefault()))
    return "$day · $time"
}

/**
 * Subtitle: customer + address as a single line. Falls back to whichever
 * one is present; returns null if neither is, so the card collapses to
 * label + title cleanly.
 */
private fun nextJobWhereLine(name: String?, address: String?): String? {
    val n = name?.takeIf { it.isNotBlank() }
    val a = address?.takeIf { it.isNotBlank() }
    return when {
        n != null && a != null -> "$n · $a"
        n != null -> n
        a != null -> a
        else -> null
    }
}

/**
 * Available-work card — customer-app design language. White surface,
 * outline-variant border, 16dp corners, brand-tinted CTA. The 56dp
 * waving-mascot avatar in the leading slot gives this card the
 * personality the earnings card below it (which has a generic icon
 * halo) doesn't have, and reinforces that the dashboard's mascot
 * character is the one "showing you" available jobs.
 *
 *  ┌────────────────────────────────────────────────┐
 *  │ ┌────┐  AVAILABLE WORK                 2 jobs  │
 *  │ │ 👋 │  Earn up to 650 Kč                       │
 *  │ └────┘                       [ Browse jobs → ] │
 *  └────────────────────────────────────────────────┘
 */
@Composable
private fun AvailableWorkHero(
    preview: AvailableJobsPreviewResponse,
    currencyCode: String?,
    onClick: () -> Unit,
) {
    val currencySymbol = remember(currencyCode) { resolveCurrencySymbol(currencyCode) }
    val jobsCount = preview.totalAvailableCount ?: 0
    val potential = preview.totalPotentialEarnings ?: 0.0

    // Layout exactly matches the customer-app's OrderAgainCard:
    // mascot (avatar) — label/title/subtitle column — trailing
    // chevron. Two text rows fit the 56dp mascot height cleanly
    // when the row is CenterVertically aligned (no more tall 3-row
    // column with a floating mascot at the top).
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = Spacing.M)
            .clip(RoundedCornerShape(20.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(20.dp),
            )
            .clickable { onClick() }
            .padding(Spacing.M + 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Image(
            painter = painterResource(R.drawable.mascot_ready),
            contentDescription = null,
            modifier = Modifier.size(72.dp),
        )
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = stringResource(R.string.dash_available_work_label),
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = stringResource(R.string.dash_available_now_count, jobsCount),
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            if (potential > 0.0) {
                Spacer(Modifier.height(2.dp))
                Text(
                    text = stringResource(
                        R.string.dash_earn_up_to,
                        formatMoneyWithSymbol(potential, currencySymbol, fallback = "—"),
                    ),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
        }
        Spacer(Modifier.width(8.dp))
        Icon(
            imageVector = Icons.AutoMirrored.Outlined.ArrowForward,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(20.dp),
        )
    }
}

@Composable
private fun EmptyHero(onClick: () -> Unit) {
    // Flat surface in customer-app design language — outline-variant
    // border + 16dp corners, no shadow. Replaces the older shadowed
    // 28dp slab so the dashboard's cards all read as one family.
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = Spacing.M)
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(16.dp),
            )
            .clickable { onClick() },
        color = MaterialTheme.colorScheme.surface,
        shape = RoundedCornerShape(16.dp),
    ) {
        Row(
            modifier = Modifier.padding(Spacing.L),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            // Friendly mascot replaces the generic work-icon block —
            // empty states are where mascots earn their keep, turning
            // a "nothing's happening" into "we're here, ready when
            // you are". Same character set the order-details tracker
            // uses, so the cleaner builds a relationship with one
            // mascot across the app.
            Image(
                painter = painterResource(R.drawable.mascot_leaning),
                contentDescription = null,
                modifier = Modifier.size(64.dp),
            )
            Spacer(Modifier.width(Spacing.M))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = stringResource(R.string.dash_no_jobs_yet_title),
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                Spacer(Modifier.height(2.dp))
                Text(
                    text = stringResource(R.string.dash_no_jobs_yet_subtitle),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.ArrowForward,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun HeroRow(icon: ImageVector? = null, text: String) {
    Row(
        verticalAlignment = Alignment.CenterVertically,
        modifier = Modifier.padding(top = 2.dp),
    ) {
        icon?.let {
            Icon(
                imageVector = it,
                contentDescription = null,
                modifier = Modifier.size(14.dp),
                tint = Color.White.copy(alpha = 0.9f),
            )
            Spacer(Modifier.width(6.dp))
        }
        Text(
            text = text,
            style = MaterialTheme.typography.bodyMedium,
            color = Color.White.copy(alpha = 0.95f),
        )
    }
}

@Composable
private fun HeroPillCta(text: String) {
    Row(
        modifier = Modifier
            .clip(CircleShape)
            .background(Color.White.copy(alpha = 0.18f))
            .padding(horizontal = Spacing.M, vertical = Spacing.XS),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = Color.White,
        )
        Spacer(Modifier.width(6.dp))
        Icon(
            imageVector = Icons.AutoMirrored.Outlined.ArrowForward,
            contentDescription = null,
            modifier = Modifier.size(16.dp),
            tint = Color.White,
        )
    }
}

/* ─── 3. Earnings split (Today / Week) ─── */

@Composable
private fun EarningsSplitRow(stats: DashboardStatsDto?, onClick: () -> Unit) {
    WeeklyEarningsHero(stats = stats, onClick = onClick)
}

/**
 * Brand-blue earnings hero. Lays out the cleaner's week-so-far as the
 * primary number, with today's earnings + the per-job average as
 * secondary inline rows. Replaces the older two-card "Today / This
 * week" split, which read as two flat siblings and showed bare
 * numbers without currency.
 *
 *  ┌─────────────────────────────────────┐
 *  │ This week               4 jobs done │
 *  │ 6 262 Kč                            │
 *  │ ─────────────────────────────────── │
 *  │ Today  1 238 Kč   ·   ~1 566 Kč/job │
 *  └─────────────────────────────────────┘
 *
 * Same gradient + corner + shadow language used by [NextJobHero] and
 * [AvailableWorkHero] above it, so the dashboard's three top cards
 * read as one family.
 */
@Composable
private fun WeeklyEarningsHero(stats: DashboardStatsDto?, onClick: () -> Unit) {
    val currencySymbol = remember(stats?.currencyCode) { resolveCurrencySymbol(stats?.currencyCode) }
    val weekAmount = stats?.weekEarnings?.toDouble() ?: 0.0
    val weekJobs = stats?.weekCompletedCount ?: 0
    val todayAmount = stats?.todayEarnings?.toDouble() ?: 0.0
    val averagePerJob = if (weekJobs > 0) weekAmount / weekJobs else 0.0

    // Customer-app design language: white surface, soft outline-
    // variant border, 16dp corners, no shadow, no gradient. Primary
    // color used only for the leading icon halo + the section label.
    // Reads as a real considered card, not a marketing slab.
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = Spacing.M)
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(16.dp),
            )
            .clickable { onClick() }
            .padding(14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        // Brand-tinted halo + small money icon — same recipe the
        // customer app's OrderAgainCard uses for its leading badge.
        Box(
            modifier = Modifier
                .size(44.dp)
                .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = Icons.AutoMirrored.Outlined.TrendingUp,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(22.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    text = stringResource(R.string.dash_earnings_week),
                    style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.primary,
                )
                Text(
                    text = if (weekJobs == 0)
                        stringResource(R.string.dash_no_completed_yet)
                    else
                        stringResource(R.string.dash_jobs_done_count, weekJobs),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Spacer(Modifier.height(2.dp))
            // titleLarge — large enough to lead the card, restrained
            // enough to live in family with the other onSurface text
            // around it. No more displaySmall ExtraBold shouting.
            Text(
                text = formatMoneyWithSymbol(weekAmount, currencySymbol, fallback = "—"),
                style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            // Single subtitle row mirrors OrderAgainCard's "title /
            // subtitle" rhythm — keeps the card to three text lines.
            val todayLabel = if (todayAmount > 0)
                stringResource(R.string.dash_earnings_today) + " " +
                    formatMoneyWithSymbol(todayAmount, currencySymbol, fallback = "—")
            else
                stringResource(R.string.dash_no_jobs_today_short)
            val avgLabel = if (averagePerJob > 0.0)
                stringResource(R.string.dash_avg_per_job) + " " +
                    formatMoneyWithSymbol(averagePerJob, currencySymbol, fallback = "—")
            else null
            val subtitle = if (avgLabel != null) "$todayLabel  ·  $avgLabel" else todayLabel
            Spacer(Modifier.height(4.dp))
            Text(
                text = subtitle,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            Spacer(Modifier.height(8.dp))
            // Explicit "view details" affordance so the card reads as a
            // tappable drill-down into the Pay & Earnings screen, not a
            // static stat block.
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = stringResource(R.string.dash_earnings_view_details),
                    style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.primary,
                )
                Spacer(Modifier.width(4.dp))
                Icon(
                    imageVector = Icons.AutoMirrored.Outlined.ArrowForward,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(16.dp),
                )
            }
        }
    }
}

/**
 * "1 234 Kč" with thin-space thousand separators. Returns
 * [fallback] when amount is null/zero so the hero can swap in a
 * "no jobs yet" message instead of rendering "0 Kč" or "—".
 *
 * [symbol] should be the user's preferred-currency symbol resolved
 * via [resolveCurrencySymbol] — that helper accepts the server-
 * supplied ISO code (e.g. "CZK") and falls back to device locale
 * only when the server hasn't told us the user's currency.
 */
private fun formatMoneyWithSymbol(amount: Double, symbol: String, fallback: String): String {
    if (amount <= 0.0) return fallback
    val rounded = String.format(Locale.getDefault(), "%,.0f", amount).replace(',', ' ')
    return if (symbol.isBlank()) rounded else "$rounded $symbol"
}

/**
 * Resolve a display symbol from the server-supplied ISO currency code
 * (e.g. "CZK" → "Kč", "EUR" → "€", "USD" → "$"). Falls back to the
 * device-locale currency symbol only when the server didn't send a
 * code — and as a last resort to an empty string.
 *
 * Why the server code is authoritative: the cleaner's preferred
 * currency lives on the Employee record and is locale-independent.
 * A Czech cleaner running a US-locale emulator should still see "Kč",
 * not "$".
 */
private fun resolveCurrencySymbol(serverCode: String?): String {
    val code = serverCode?.takeIf { it.isNotBlank() }
    if (code != null) {
        return runCatching {
            java.util.Currency.getInstance(code).getSymbol(Locale.getDefault())
        }.getOrDefault(code)
    }
    return runCatching {
        java.util.Currency.getInstance(Locale.getDefault()).symbol
    }.getOrDefault("")
}

/* ─── 4. Pay period ─── */

@Composable
private fun PayPeriodCard(stats: DashboardStatsDto, onClick: () -> Unit) {
    val start = parseUtcDate(stats.currentPayPeriodStart) ?: return
    val end = parseUtcDate(stats.currentPayPeriodEnd) ?: return
    val today = LocalDate.now()

    val totalDays = (ChronoUnit.DAYS.between(start, end).toInt() + 1).coerceAtLeast(1)
    val dayIndex = (ChronoUnit.DAYS.between(start, today).toInt() + 1).coerceIn(1, totalDays)
    val progress = dayIndex.toFloat() / totalDays.toFloat()

    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = Spacing.M)
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(16.dp),
            )
            .clickable { onClick() },
        color = MaterialTheme.colorScheme.surface,
        shape = RoundedCornerShape(16.dp),
    ) {
        Column(modifier = Modifier.padding(Spacing.M)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    text = stringResource(R.string.dash_current_period),
                    style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Text(
                    text = stringResource(R.string.dash_pay_period_progress, dayIndex, totalDays),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Spacer(Modifier.height(Spacing.XS))
            Text(
                text = formatMoney(stats.currentPeriodEarnings?.toDouble()),
                style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Spacer(Modifier.height(Spacing.S))
            LinearProgressIndicator(
                progress = { progress },
                modifier = Modifier
                    .fillMaxWidth()
                    .height(6.dp)
                    .clip(RoundedCornerShape(3.dp)),
                color = MaterialTheme.colorScheme.primary,
                trackColor = MaterialTheme.colorScheme.primary.copy(alpha = 0.12f),
                drawStopIndicator = {},
            )
            stats.nextPayoutDate?.let { isoPayout ->
                Spacer(Modifier.height(Spacing.S))
                Text(
                    text = stringResource(R.string.dash_next_payout, formatPayoutDate(isoPayout)),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

/* ─── 5. Last month ─── */

@Composable
private fun LastMonthCard(stats: DashboardStatsDto?) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = Spacing.M)
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(16.dp),
            ),
        color = MaterialTheme.colorScheme.surface,
        shape = RoundedCornerShape(16.dp),
    ) {
        Column(modifier = Modifier.padding(Spacing.M)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    text = stringResource(R.string.dash_last_month_section),
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                MonthDeltaChip(
                    thisMonth = stats?.thisMonthCompletedOrders ?: 0,
                    lastMonth = stats?.lastMonthCompletedOrders ?: 0,
                )
            }
            Spacer(Modifier.height(Spacing.M))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                MetricColumn(
                    value = formatMoney(stats?.lastMonthEarnings?.toDouble()),
                    label = stringResource(R.string.dash_last_month_earnings),
                )
                MetricColumn(
                    value = (stats?.lastMonthCompletedOrders ?: 0).toString(),
                    label = stringResource(R.string.dash_last_month_jobs),
                )
                RatingColumn(stats = stats)
            }
        }
    }
}

@Composable
private fun MetricColumn(value: String, label: String) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text(
            text = value,
            style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
            color = MaterialTheme.colorScheme.onSurface,
        )
        Spacer(Modifier.height(2.dp))
        Text(
            text = label,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun RatingColumn(stats: DashboardStatsDto?) {
    val avg = stats?.averageRating
    val count = stats?.ratingCount ?: 0
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(
                imageVector = Icons.Outlined.Star,
                contentDescription = null,
                modifier = Modifier.size(20.dp),
                tint = MaterialTheme.colorScheme.primary,
            )
            Spacer(Modifier.width(4.dp))
            Text(
                text = if (avg == null) "—"
                else String.format(Locale.getDefault(), "%.1f", avg),
                style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
            )
        }
        Spacer(Modifier.height(2.dp))
        Text(
            text = if (count == 0) stringResource(R.string.dash_no_rating_yet)
            else stringResource(R.string.dash_rating_count, count),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun MonthDeltaChip(thisMonth: Int, lastMonth: Int) {
    val delta = computeMonthDelta(thisMonth, lastMonth) ?: return
    val up = delta >= 0
    val color = if (up) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error
    Row(
        modifier = Modifier
            .clip(CircleShape)
            .background(color.copy(alpha = 0.12f))
            .padding(horizontal = 8.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = if (up) Icons.AutoMirrored.Outlined.TrendingUp else Icons.AutoMirrored.Outlined.TrendingDown,
            contentDescription = null,
            modifier = Modifier.size(14.dp),
            tint = color,
        )
        Spacer(Modifier.width(4.dp))
        Text(
            text = "${if (up) "+" else ""}$delta%",
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = color,
        )
    }
}

/* ─── 6. Quick actions ─── */

@Composable
private fun QuickActionsGrid(
    onProfile: () -> Unit,
    onPayHistory: () -> Unit,
    onDocuments: () -> Unit,
    onHelp: () -> Unit,
) {
    Column(modifier = Modifier.fillMaxWidth().padding(horizontal = Spacing.M)) {
        Text(
            text = stringResource(R.string.dash_quick_actions),
            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onBackground,
            modifier = Modifier.padding(start = Spacing.XS, bottom = Spacing.S),
        )
        Row(horizontalArrangement = Arrangement.spacedBy(Spacing.S)) {
            QuickActionTile(
                modifier = Modifier.weight(1f),
                icon = Icons.Outlined.Person,
                label = stringResource(R.string.dash_qa_profile),
                onClick = onProfile,
            )
            QuickActionTile(
                modifier = Modifier.weight(1f),
                icon = Icons.Outlined.History,
                label = stringResource(R.string.dash_qa_pay_history),
                onClick = onPayHistory,
            )
            QuickActionTile(
                modifier = Modifier.weight(1f),
                icon = Icons.AutoMirrored.Outlined.Article,
                label = stringResource(R.string.dash_qa_documents),
                onClick = onDocuments,
            )
            QuickActionTile(
                modifier = Modifier.weight(1f),
                icon = Icons.AutoMirrored.Outlined.HelpOutline,
                label = stringResource(R.string.dash_qa_help),
                onClick = onHelp,
            )
        }
    }
}

@Composable
private fun QuickActionTile(
    modifier: Modifier = Modifier,
    icon: ImageVector,
    label: String,
    onClick: () -> Unit,
) {
    Surface(
        modifier = modifier
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.outlineVariant,
                shape = RoundedCornerShape(16.dp),
            )
            .clickable { onClick() },
        color = MaterialTheme.colorScheme.surface,
        shape = RoundedCornerShape(16.dp),
    ) {
        Column(
            modifier = Modifier.padding(vertical = Spacing.M, horizontal = Spacing.XS),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Box(
                modifier = Modifier
                    .size(40.dp)
                    .clip(RoundedCornerShape(12.dp))
                    .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(20.dp),
                )
            }
            Spacer(Modifier.height(Spacing.XS))
            Text(
                text = label,
                style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.Medium),
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 2,
            )
        }
    }
}

/* ─── Pure derivations ─── */

private fun pickNextJob(upcoming: List<OrderListItem>): OrderListItem? {
    val active = setOf(OrderStatus._2, OrderStatus._3, OrderStatus._4)
    return upcoming
        .filter { it.orderStatus.toOrderStatus() in active }
        .minByOrNull { it.cleaningDateTime ?: "" }
}

private fun filterTodaysJobs(upcoming: List<OrderListItem>): List<OrderListItem> {
    val today = LocalDate.now()
    return upcoming.filter { item ->
        val iso = item.cleaningDateTime ?: return@filter false
        val local = runCatching {
            Instant.parse(iso).atZone(ZoneId.systemDefault()).toLocalDate()
        }.getOrNull()
        local == today
    }
}

private fun computeMonthDelta(thisMonth: Int, lastMonth: Int): Int? {
    if (lastMonth == 0) return if (thisMonth > 0) 100 else null
    return ((thisMonth - lastMonth).toDouble() / lastMonth.toDouble() * 100.0).toInt()
}

@Composable
private fun nextJobLabelAndCta(status: OrderStatus?): Pair<String, String> = when (status) {
    OrderStatus._4 -> stringResource(R.string.dash_in_progress) to stringResource(R.string.dash_continue)
    OrderStatus._3 -> stringResource(R.string.dash_on_the_way) to stringResource(R.string.dash_open)
    else -> stringResource(R.string.dash_next_job) to stringResource(R.string.dash_open)
}

private fun countdown(iso: String?): String {
    if (iso.isNullOrBlank()) return "—"
    val cleaning = runCatching { Instant.parse(iso) }.getOrNull() ?: return iso
    val now = Instant.now()
    val duration = Duration.between(now, cleaning)
    if (duration.isNegative) return formatDateTime(iso)
    val hours = duration.toHours()
    val minutes = (duration.toMinutes() % 60)
    return when {
        hours >= 24 -> formatDateTime(iso)
        hours >= 1 -> String.format(Locale.getDefault(), "In %dh %02dm", hours, minutes)
        else -> String.format(Locale.getDefault(), "In %dm", duration.toMinutes())
    }
}

@Composable
private fun greeting(firstName: String?): String {
    val hour = remember { java.time.LocalTime.now().hour }
    val name = firstName?.takeIf { it.isNotBlank() }
    return when {
        hour < 12 && name != null -> stringResource(R.string.good_morning_name, name)
        hour < 18 && name != null -> stringResource(R.string.good_afternoon_name, name)
        name != null -> stringResource(R.string.good_evening_name, name)
        hour < 12 -> stringResource(R.string.good_morning)
        hour < 18 -> stringResource(R.string.good_afternoon)
        else -> stringResource(R.string.good_evening)
    }
}

private fun formatMoney(amount: Double?): String {
    if (amount == null || amount == 0.0) return "—"
    return String.format(Locale.getDefault(), "%.0f", amount)
}

private fun formatDateTime(iso: String): String = runCatching {
    val instant = Instant.parse(iso)
    val local = instant.atZone(ZoneId.systemDefault())
    DateTimeFormatter
        .ofLocalizedDateTime(FormatStyle.MEDIUM, FormatStyle.SHORT)
        .withLocale(Locale.getDefault())
        .format(local)
}.getOrDefault(iso)

private fun formatPayoutDate(iso: String): String = runCatching {
    val instant = Instant.parse(iso)
    val local = instant.atZone(ZoneId.systemDefault()).toLocalDate()
    local.format(DateTimeFormatter.ofPattern("EEE, d MMM", Locale.getDefault()))
}.getOrDefault(iso)

private fun parseUtcDate(iso: String?): LocalDate? {
    if (iso.isNullOrBlank()) return null
    return runCatching {
        Instant.parse(iso).atZone(ZoneId.systemDefault()).toLocalDate()
    }.getOrNull()
}
