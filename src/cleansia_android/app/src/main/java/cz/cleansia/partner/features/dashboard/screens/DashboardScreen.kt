package cz.cleansia.partner.features.dashboard.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import cz.cleansia.partner.ui.theme.LocalDarkTheme
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.automirrored.filled.ListAlt
import androidx.compose.material.icons.filled.AccessTime
import androidx.compose.material.icons.filled.AttachMoney
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.KeyboardArrowUp
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.dashboard.TrendDirection
import cz.cleansia.partner.domain.models.dashboard.UpcomingOrder
import cz.cleansia.partner.features.dashboard.viewmodels.DashboardUiState
import cz.cleansia.partner.features.dashboard.viewmodels.DashboardViewModel
import cz.cleansia.partner.features.dashboard.viewmodels.GreetingType
import cz.cleansia.partner.ui.components.ErrorView
import cz.cleansia.partner.ui.components.LoadingIndicator
import cz.cleansia.partner.ui.components.OrderStatusBadge
import cz.cleansia.partner.ui.components.SparklineChart
import cz.cleansia.partner.ui.theme.CleansiaColors
import cz.cleansia.partner.core.utils.DateTimeUtils
import java.text.NumberFormat
import java.time.Duration
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.LocalTime
import java.time.format.DateTimeFormatter
import java.util.Currency
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardScreen(
    onNavigateToOrderDetails: (String) -> Unit,
    onNavigateToOrders: () -> Unit = {},
    onNavigateToAvailableOrders: () -> Unit = onNavigateToOrders,
    onNavigateToActiveOrders: () -> Unit = onNavigateToOrders,
    onNavigateToCompletedOrders: () -> Unit = onNavigateToOrders,
    onNavigateToInvoices: () -> Unit = {},
    onNavigateToAnalytics: () -> Unit = {},
    onScrolled: (Boolean) -> Unit = {},
    viewModel: DashboardViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    // Show error in snackbar
    LaunchedEffect(uiState.error) {
        uiState.error?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    // Build personalized greeting: "Good evening, Mike" or fallback to "Good evening!"
    val greetingText = if (uiState.userName.isNotBlank()) {
        when (uiState.greeting) {
            GreetingType.MORNING -> stringResource(R.string.good_morning_name, uiState.userName)
            GreetingType.AFTERNOON -> stringResource(R.string.good_afternoon_name, uiState.userName)
            GreetingType.EVENING -> stringResource(R.string.good_evening_name, uiState.userName)
        }
    } else {
        when (uiState.greeting) {
            GreetingType.MORNING -> stringResource(R.string.good_morning)
            GreetingType.AFTERNOON -> stringResource(R.string.good_afternoon)
            GreetingType.EVENING -> stringResource(R.string.good_evening)
        }
    }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) }
    ) { paddingValues ->
        when {
            uiState.isLoading && uiState.stats == null -> {
                LoadingIndicator(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(paddingValues)
                )
            }
            uiState.error != null && uiState.stats == null -> {
                ErrorView(
                    message = uiState.error ?: "Unknown error",
                    onRetry = { viewModel.loadDashboardData() },
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(paddingValues)
                )
            }
            else -> {
                PullToRefreshBox(
                    isRefreshing = uiState.isRefreshing,
                    onRefresh = { viewModel.refresh() },
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(paddingValues)
                ) {
                    DashboardContent(
                        uiState = uiState,
                        greetingText = greetingText,
                        onOrderClick = onNavigateToOrderDetails,
                        onNavigateToOrders = onNavigateToOrders,
                        onNavigateToAvailableOrders = onNavigateToAvailableOrders,
                        onNavigateToActiveOrders = onNavigateToActiveOrders,
                        onNavigateToCompletedOrders = onNavigateToCompletedOrders,
                        onNavigateToInvoices = onNavigateToInvoices,
                        onNavigateToAnalytics = onNavigateToAnalytics,
                        onScrolled = onScrolled
                    )
                }
            }
        }
    }
}

@Composable
private fun DashboardContent(
    uiState: DashboardUiState,
    greetingText: String,
    onOrderClick: (String) -> Unit,
    onNavigateToOrders: () -> Unit,
    onNavigateToAvailableOrders: () -> Unit,
    onNavigateToActiveOrders: () -> Unit,
    onNavigateToCompletedOrders: () -> Unit,
    onNavigateToInvoices: () -> Unit,
    onNavigateToAnalytics: () -> Unit = {},
    onScrolled: (Boolean) -> Unit = {}
) {
    val listState = rememberLazyListState()

    // Report scroll state to parent
    val isScrolled by remember {
        derivedStateOf { listState.firstVisibleItemIndex > 0 || listState.firstVisibleItemScrollOffset > 0 }
    }
    LaunchedEffect(isScrolled) {
        onScrolled(isScrolled)
    }

    LazyColumn(
        state = listState,
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 64.dp, bottom = 100.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // === Greeting Hero ===
        item {
            GreetingHero(
                greetingText = greetingText,
                activeOrders = uiState.stats?.myActiveOrders ?: 0,
                availableOrders = uiState.stats?.availableOrders ?: 0
            )
        }

        // === Quick Stats Row (horizontal scroll) ===
        item {
            QuickStatsRow(
                availableOrders = uiState.stats?.availableOrders ?: 0,
                activeOrders = uiState.stats?.myActiveOrders ?: 0,
                completedThisMonth = uiState.stats?.completedThisMonth ?: 0,
                completionTrend = uiState.stats?.completionTrend,
                pendingEarnings = uiState.stats?.pendingEarnings ?: 0.0,
                currency = uiState.stats?.currency ?: "CZK",
                onAvailableOrdersClick = onNavigateToAvailableOrders,
                onActiveOrdersClick = onNavigateToActiveOrders,
                onCompletedClick = onNavigateToCompletedOrders,
                onEarningsClick = onNavigateToInvoices
            )
        }

        // === Next Up - Featured upcoming order ===
        if (uiState.upcomingOrders.isNotEmpty()) {
            item {
                NextUpCard(
                    order = uiState.upcomingOrders.first(),
                    onClick = { onOrderClick(uiState.upcomingOrders.first().id) }
                )
            }
        }

        // === Completion Rate ===
        if (uiState.stats != null) {
            val totalOrders = (uiState.stats.completedThisMonth + (uiState.stats.myActiveOrders))
            if (totalOrders > 0) {
                item {
                    CompletionRateCard(
                        completed = uiState.stats.completedThisMonth,
                        total = totalOrders
                    )
                }
            }
        }

        // === Earnings Overview ===
        item {
            uiState.earnings?.let { earnings ->
                EarningsOverviewCard(
                    thisWeek = earnings.thisWeek,
                    thisMonth = earnings.thisMonth,
                    lastMonth = earnings.lastMonth,
                    currency = earnings.currency,
                    onClick = onNavigateToAnalytics
                )
            }
        }

        // === Upcoming Orders ===
        if (uiState.upcomingOrders.size > 1) {
            item {
                SectionHeaderWithAction(
                    title = stringResource(R.string.upcoming_orders),
                    actionLabel = stringResource(R.string.view_all),
                    onActionClick = onNavigateToOrders
                )
            }

            items(uiState.upcomingOrders.drop(1).take(4), key = { it.id }) { order ->
                UpcomingOrderCard(
                    order = order,
                    onClick = { onOrderClick(order.id) }
                )
            }
        } else if (uiState.upcomingOrders.isEmpty()) {
            item {
                SectionHeaderWithAction(
                    title = stringResource(R.string.upcoming_orders),
                    actionLabel = stringResource(R.string.view_all),
                    onActionClick = onNavigateToOrders
                )
            }
            item {
                EmptyStateCard(
                    message = stringResource(R.string.no_upcoming_orders)
                )
            }
        }
    }
}

// ============================================================
// Greeting Hero
// ============================================================

@Composable
private fun GreetingHero(
    greetingText: String,
    activeOrders: Int,
    availableOrders: Int
) {
    val isDarkTheme = LocalDarkTheme.current
    val bgGradientStart = if (isDarkTheme) Color(0xFF1E293B) else Color(0xFFE0F2FE)
    val bgGradientEnd = if (isDarkTheme) Color(0xFF0F172A) else Color(0xFFF0F9FF)
    val textColor = if (isDarkTheme) Color(0xFFE0F2FE) else Color(0xFF0C4A6E)
    val subtextColor = if (isDarkTheme) Color(0xFF94A3B8) else Color(0xFF475569)

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(
                Brush.linearGradient(
                    colors = listOf(bgGradientStart, bgGradientEnd)
                )
            )
            .padding(20.dp)
    ) {
        Column {
            Text(
                text = greetingText,
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold,
                color = textColor
            )
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = stringResource(R.string.dashboard_subtitle),
                style = MaterialTheme.typography.bodyMedium,
                color = subtextColor
            )
        }
    }
}

// ============================================================
// Quick Stats Row (horizontal scroll)
// ============================================================

@Composable
private fun QuickStatsRow(
    availableOrders: Int,
    activeOrders: Int,
    completedThisMonth: Int,
    completionTrend: cz.cleansia.partner.domain.models.dashboard.TrendData?,
    pendingEarnings: Double,
    currency: String,
    onAvailableOrdersClick: () -> Unit,
    onActiveOrdersClick: () -> Unit,
    onCompletedClick: () -> Unit,
    onEarningsClick: () -> Unit
) {
    Column(
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        // First row
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            StatCard(
                title = stringResource(R.string.available_orders),
                value = availableOrders.toString(),
                icon = Icons.AutoMirrored.Filled.ListAlt,
                iconBackgroundColor = CleansiaColors.infoContainer,
                iconColor = CleansiaColors.onInfoContainer,
                onClick = onAvailableOrdersClick,
                modifier = Modifier.weight(1f)
            )
            StatCard(
                title = stringResource(R.string.my_active_orders),
                value = activeOrders.toString(),
                icon = Icons.Default.PlayArrow,
                iconBackgroundColor = CleansiaColors.warningContainer,
                iconColor = CleansiaColors.onWarningContainer,
                onClick = onActiveOrdersClick,
                modifier = Modifier.weight(1f)
            )
        }

        // Second row
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            StatCard(
                title = stringResource(R.string.completed_this_month),
                value = completedThisMonth.toString(),
                icon = Icons.Default.CheckCircle,
                iconBackgroundColor = CleansiaColors.successContainer,
                iconColor = CleansiaColors.onSuccessContainer,
                onClick = onCompletedClick,
                trend = completionTrend,
                modifier = Modifier.weight(1f)
            )
            StatCard(
                title = stringResource(R.string.pending_earnings),
                value = formatCurrency(pendingEarnings, currency),
                icon = Icons.Default.AttachMoney,
                iconBackgroundColor = MaterialTheme.colorScheme.primaryContainer,
                iconColor = MaterialTheme.colorScheme.onPrimaryContainer,
                onClick = onEarningsClick,
                modifier = Modifier.weight(1f)
            )
        }
    }
}

@Composable
private fun StatCard(
    title: String,
    value: String,
    icon: ImageVector,
    iconBackgroundColor: Color,
    iconColor: Color,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    trend: cz.cleansia.partner.domain.models.dashboard.TrendData? = null
) {
    Card(
        modifier = modifier
            .height(120.dp)
            .clickable { onClick() },
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(12.dp),
            verticalArrangement = Arrangement.SpaceBetween
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.Top
            ) {
                Box(
                    modifier = Modifier
                        .size(36.dp)
                        .clip(CircleShape)
                        .background(iconBackgroundColor),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = icon,
                        contentDescription = null,
                        tint = iconColor,
                        modifier = Modifier.size(20.dp)
                    )
                }

                if (trend != null) {
                    TrendIndicator(trend = trend)
                } else {
                    Icon(
                        imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.5f),
                        modifier = Modifier.size(16.dp)
                    )
                }
            }

            Column {
                Text(
                    text = value,
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    fontSize = 18.sp
                )
                Text(
                    text = title,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
            }
        }
    }
}

@Composable
private fun TrendIndicator(trend: cz.cleansia.partner.domain.models.dashboard.TrendData) {
    val (icon, color) = when (trend.direction) {
        TrendDirection.UP -> Icons.Default.KeyboardArrowUp to CleansiaColors.success
        TrendDirection.DOWN -> Icons.Default.KeyboardArrowDown to MaterialTheme.colorScheme.error
        TrendDirection.NEUTRAL -> Icons.Default.Remove to MaterialTheme.colorScheme.onSurfaceVariant
    }

    Row(
        verticalAlignment = Alignment.CenterVertically,
        modifier = Modifier
            .clip(RoundedCornerShape(12.dp))
            .background(color.copy(alpha = 0.1f))
            .padding(horizontal = 6.dp, vertical = 2.dp)
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = color,
            modifier = Modifier.size(14.dp)
        )
        if (trend.percentage > 0) {
            Text(
                text = "${trend.percentage.toInt()}%",
                style = MaterialTheme.typography.labelSmall,
                color = color,
                fontWeight = FontWeight.Medium
            )
        }
    }
}

// ============================================================
// Earnings Overview
// ============================================================

@Composable
private fun EarningsOverviewCard(
    thisWeek: Double,
    thisMonth: Double,
    lastMonth: Double,
    currency: String,
    onClick: () -> Unit = {}
) {
    // Generate sparkline data based on the earnings values
    val sparklineData = remember(thisWeek, thisMonth, lastMonth) {
        listOf(
            lastMonth.toFloat() * 0.7f,
            lastMonth.toFloat() * 0.85f,
            lastMonth.toFloat(),
            thisMonth.toFloat() * 0.6f,
            thisMonth.toFloat() * 0.8f,
            thisMonth.toFloat()
        )
    }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable { onClick() },
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.primaryContainer
        )
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = stringResource(R.string.earnings_overview),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onPrimaryContainer
                )

                // Mini sparkline chart
                SparklineChart(
                    data = sparklineData,
                    lineColor = MaterialTheme.colorScheme.onPrimaryContainer,
                    fillColor = MaterialTheme.colorScheme.onPrimaryContainer.copy(alpha = 0.1f),
                    modifier = Modifier
                        .width(80.dp)
                        .height(32.dp)
                )
            }

            Spacer(modifier = Modifier.height(16.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceEvenly
            ) {
                EarningsItem(
                    label = stringResource(R.string.this_week),
                    amount = thisWeek,
                    currency = currency,
                    modifier = Modifier.weight(1f)
                )
                EarningsItem(
                    label = stringResource(R.string.this_month),
                    amount = thisMonth,
                    currency = currency,
                    modifier = Modifier.weight(1f)
                )
                EarningsItem(
                    label = stringResource(R.string.last_month),
                    amount = lastMonth,
                    currency = currency,
                    modifier = Modifier.weight(1f)
                )
            }
        }
    }
}

@Composable
private fun EarningsItem(
    label: String,
    amount: Double,
    currency: String,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = formatCurrency(amount, currency),
            style = MaterialTheme.typography.titleSmall,
            fontWeight = FontWeight.Bold,
            color = MaterialTheme.colorScheme.onPrimaryContainer,
            textAlign = TextAlign.Center,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onPrimaryContainer.copy(alpha = 0.7f),
            textAlign = TextAlign.Center,
            maxLines = 1
        )
    }
}

// ============================================================
// Next Up - Featured Upcoming Order
// ============================================================

@Composable
private fun NextUpCard(
    order: UpcomingOrder,
    onClick: () -> Unit
) {
    val isDarkTheme = LocalDarkTheme.current
    val containerColor = if (isDarkTheme) Color(0xFF1A2740) else Color(0xFFEFF6FF)
    val accentColor = if (isDarkTheme) Color(0xFF60A5FA) else Color(0xFF2563EB)

    // Compute time label
    val timeLabel = remember(order.scheduledDate, order.scheduledTime) {
        computeTimeLabel(order.scheduledDate, order.scheduledTime)
    }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable { onClick() },
        colors = CardDefaults.cardColors(containerColor = containerColor),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        modifier = Modifier
                            .size(32.dp)
                            .clip(CircleShape)
                            .background(accentColor.copy(alpha = 0.15f)),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = Icons.Default.AccessTime,
                            contentDescription = null,
                            tint = accentColor,
                            modifier = Modifier.size(18.dp)
                        )
                    }
                    Spacer(modifier = Modifier.width(8.dp))
                    Column {
                        Text(
                            text = stringResource(R.string.next_up),
                            style = MaterialTheme.typography.labelMedium,
                            fontWeight = FontWeight.Bold,
                            color = accentColor
                        )
                        if (timeLabel != null) {
                            Text(
                                text = timeLabel,
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                }

                Text(
                    text = formatCurrency(order.totalAmount, order.currency),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = accentColor
                )
            }

            Spacer(modifier = Modifier.height(12.dp))

            // Order details
            Text(
                text = "#${order.orderNumber ?: order.id.take(8)}",
                style = MaterialTheme.typography.titleSmall,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )

            Spacer(modifier = Modifier.height(4.dp))

            order.customerName?.let { name ->
                Text(
                    text = name,
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }

            order.address?.let { address ->
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Default.LocationOn,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(14.dp)
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Text(
                        text = address,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }
            }

            order.servicesPreview?.let { services ->
                Spacer(modifier = Modifier.height(6.dp))
                ServiceChip(serviceName = services)
            }
        }
    }
}

private fun computeTimeLabel(scheduledDate: String?, scheduledTime: String?): String? {
    if (scheduledDate == null) return null
    return try {
        val date = LocalDate.parse(scheduledDate, DateTimeFormatter.ofPattern("yyyy-MM-dd"))
        val today = LocalDate.now()
        val time = scheduledTime?.let {
            try { LocalTime.parse(it) } catch (_: Exception) { null }
        }

        val timeStr = time?.format(DateTimeFormatter.ofPattern("HH:mm")) ?: ""

        when {
            date.isEqual(today) && time != null -> {
                val now = LocalDateTime.now()
                val scheduled = LocalDateTime.of(date, time)
                val diff = Duration.between(now, scheduled)
                if (diff.isNegative) "Today at $timeStr"
                else {
                    val hours = diff.toHours()
                    val minutes = diff.toMinutes() % 60
                    when {
                        hours > 0 -> "In ${hours}h ${minutes}min"
                        minutes > 0 -> "In ${minutes}min"
                        else -> "Now"
                    }
                }
            }
            date.isEqual(today) -> "Today"
            date.isEqual(today.plusDays(1)) && timeStr.isNotEmpty() -> "Tomorrow at $timeStr"
            date.isEqual(today.plusDays(1)) -> "Tomorrow"
            else -> DateTimeUtils.formatDate(scheduledDate) + if (timeStr.isNotEmpty()) " • $timeStr" else ""
        }
    } catch (_: Exception) {
        null
    }
}

// ============================================================
// Completion Rate Card
// ============================================================

@Composable
private fun CompletionRateCard(
    completed: Int,
    total: Int
) {
    val fraction = if (total > 0) completed.toFloat() / total.toFloat() else 0f

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = stringResource(R.string.completion_rate),
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Text(
                    text = "${(fraction * 100).toInt()}%",
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.Bold,
                    color = CleansiaColors.success
                )
            }

            Spacer(modifier = Modifier.height(8.dp))

            // Progress bar
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(8.dp)
                    .clip(RoundedCornerShape(4.dp))
                    .background(MaterialTheme.colorScheme.surfaceVariant)
            ) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth(fraction.coerceIn(0f, 1f))
                        .height(8.dp)
                        .clip(RoundedCornerShape(4.dp))
                        .background(CleansiaColors.success)
                )
            }

            Spacer(modifier = Modifier.height(4.dp))

            Text(
                text = stringResource(R.string.completed_vs_total, completed, total),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

// ============================================================
// Section Headers
// ============================================================

@Composable
private fun SectionHeaderWithAction(
    title: String,
    actionLabel: String,
    onActionClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = title,
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.onBackground
        )
        TextButton(onClick = onActionClick) {
            Text(
                text = actionLabel,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.primary
            )
            Spacer(modifier = Modifier.width(4.dp))
            Icon(
                imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                contentDescription = null,
                modifier = Modifier.size(16.dp),
                tint = MaterialTheme.colorScheme.primary
            )
        }
    }
}

// ============================================================
// Upcoming Order Card
// ============================================================

@Composable
private fun UpcomingOrderCard(
    order: UpcomingOrder,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable { onClick() },
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "#${order.orderNumber ?: order.id.take(8)}",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )

                Text(
                    text = formatCurrency(order.totalAmount, order.currency),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary,
                    maxLines = 1
                )
            }

            Spacer(modifier = Modifier.height(6.dp))

            // Status badge on its own row
            OrderStatusBadge(
                status = cz.cleansia.partner.domain.models.orders.OrderStatus.fromApiName(order.status)
            )

            Spacer(modifier = Modifier.height(8.dp))

            // Customer name
            order.customerName?.let { name ->
                Text(
                    text = name,
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.Medium,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Spacer(modifier = Modifier.height(4.dp))
            }

            // Address
            order.address?.let { address ->
                Row(
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(
                        imageVector = Icons.Default.LocationOn,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(16.dp)
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Text(
                        text = address,
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }
                Spacer(modifier = Modifier.height(4.dp))
            }

            // Scheduled date/time
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
                    text = buildString {
                        append(DateTimeUtils.formatDate(order.scheduledDate))
                        order.scheduledTime?.let { time ->
                            append(" \u2022 ")
                            append(DateTimeUtils.formatTimeOnly(time))
                        }
                    },
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            // Services preview
            order.servicesPreview?.let { servicesText ->
                Spacer(modifier = Modifier.height(8.dp))
                ServiceChip(serviceName = servicesText)
            }
        }
    }
}

@Composable
private fun ServiceChip(serviceName: String) {
    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.secondaryContainer)
            .padding(horizontal = 8.dp, vertical = 4.dp)
    ) {
        Text(
            text = serviceName,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSecondaryContainer
        )
    }
}

@Composable
private fun EmptyStateCard(message: String) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceVariant
        )
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .padding(32.dp),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = message,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center
            )
        }
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
