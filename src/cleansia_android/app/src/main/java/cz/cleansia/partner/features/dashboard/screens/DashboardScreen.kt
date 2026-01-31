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
import androidx.compose.foundation.lazy.LazyRow
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
import cz.cleansia.partner.ui.components.ChartLegendItem
import cz.cleansia.partner.ui.components.DonutChart
import cz.cleansia.partner.ui.components.DonutChartData
import cz.cleansia.partner.ui.components.ErrorView
import cz.cleansia.partner.ui.components.LoadingIndicator
import cz.cleansia.partner.ui.components.OrderStatusBadge
import cz.cleansia.partner.ui.components.SparklineChart
import cz.cleansia.partner.ui.theme.CleansiaColors
import cz.cleansia.partner.core.utils.DateTimeUtils
import java.text.NumberFormat
import java.util.Currency
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardScreen(
    onNavigateToOrderDetails: (String) -> Unit,
    onNavigateToOrders: () -> Unit = {},
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
                onAvailableOrdersClick = onNavigateToOrders,
                onActiveOrdersClick = onNavigateToOrders,
                onCompletedClick = onNavigateToOrders,
                onEarningsClick = onNavigateToInvoices
            )
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

        // === Order Distribution Chart ===
        uiState.stats?.let { stats ->
            if (stats.availableOrders > 0 || stats.myActiveOrders > 0 || stats.completedThisMonth > 0) {
                item {
                    OrderDistributionCard(
                        availableOrders = stats.availableOrders,
                        activeOrders = stats.myActiveOrders,
                        completedOrders = stats.completedThisMonth
                    )
                }
            }
        }

        // === Upcoming Orders ===
        item {
            SectionHeaderWithAction(
                title = stringResource(R.string.upcoming_orders),
                actionLabel = stringResource(R.string.view_all),
                onActionClick = onNavigateToOrders
            )
        }

        if (uiState.upcomingOrders.isEmpty()) {
            item {
                EmptyStateCard(
                    message = stringResource(R.string.no_upcoming_orders)
                )
            }
        } else {
            items(uiState.upcomingOrders.take(5), key = { it.id }) { order ->
                UpcomingOrderCard(
                    order = order,
                    onClick = { onOrderClick(order.id) }
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

            // Quick summary chips
            if (activeOrders > 0 || availableOrders > 0) {
                Spacer(modifier = Modifier.height(16.dp))
                Row(
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    if (activeOrders > 0) {
                        SummaryChip(
                            label = "$activeOrders ${stringResource(R.string.active).lowercase()}",
                            backgroundColor = CleansiaColors.warningContainer,
                            textColor = CleansiaColors.onWarningContainer
                        )
                    }
                    if (availableOrders > 0) {
                        SummaryChip(
                            label = "$availableOrders ${stringResource(R.string.available).lowercase()}",
                            backgroundColor = CleansiaColors.infoContainer,
                            textColor = CleansiaColors.onInfoContainer
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun SummaryChip(
    label: String,
    backgroundColor: Color,
    textColor: Color
) {
    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(20.dp))
            .background(backgroundColor)
            .padding(horizontal = 12.dp, vertical = 6.dp)
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelMedium,
            fontWeight = FontWeight.Medium,
            color = textColor
        )
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
            StatCardClickable(
                title = stringResource(R.string.available_orders),
                value = availableOrders.toString(),
                icon = Icons.AutoMirrored.Filled.ListAlt,
                iconBackgroundColor = CleansiaColors.infoContainer,
                iconColor = CleansiaColors.onInfoContainer,
                onClick = onAvailableOrdersClick,
                modifier = Modifier.weight(1f)
            )
            StatCardClickable(
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
            StatCardWithTrend(
                title = stringResource(R.string.completed_this_month),
                value = completedThisMonth.toString(),
                icon = Icons.Default.CheckCircle,
                iconBackgroundColor = CleansiaColors.successContainer,
                iconColor = CleansiaColors.onSuccessContainer,
                trend = completionTrend,
                onClick = onCompletedClick,
                modifier = Modifier.weight(1f)
            )
            StatCardClickable(
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
private fun StatCardClickable(
    title: String,
    value: String,
    icon: ImageVector,
    iconBackgroundColor: Color,
    iconColor: Color,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
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
                Icon(
                    imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.5f),
                    modifier = Modifier.size(16.dp)
                )
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
private fun StatCardWithTrend(
    title: String,
    value: String,
    icon: ImageVector,
    iconBackgroundColor: Color,
    iconColor: Color,
    trend: cz.cleansia.partner.domain.models.dashboard.TrendData?,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
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

                // Trend indicator
                trend?.let {
                    TrendIndicator(trend = it)
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
private fun OrderDistributionCard(
    availableOrders: Int,
    activeOrders: Int,
    completedOrders: Int
) {
    val total = availableOrders + activeOrders + completedOrders
    val chartData = listOf(
        DonutChartData("Available", availableOrders.toFloat(), CleansiaColors.infoContainer),
        DonutChartData("Active", activeOrders.toFloat(), CleansiaColors.warningContainer),
        DonutChartData("Completed", completedOrders.toFloat(), CleansiaColors.successContainer)
    )

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Text(
                text = stringResource(R.string.order_distribution),
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )

            Spacer(modifier = Modifier.height(16.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Donut chart
                DonutChart(
                    data = chartData,
                    modifier = Modifier.size(100.dp),
                    strokeWidth = 16.dp,
                    centerContent = {
                        Text(
                            text = total.toString(),
                            style = MaterialTheme.typography.titleLarge,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                    }
                )

                // Legend
                Column(
                    modifier = Modifier
                        .weight(1f)
                        .padding(start = 24.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    ChartLegendItem(
                        color = CleansiaColors.infoContainer,
                        label = stringResource(R.string.available),
                        value = availableOrders.toString()
                    )
                    ChartLegendItem(
                        color = CleansiaColors.warningContainer,
                        label = stringResource(R.string.my_active_orders),
                        value = activeOrders.toString()
                    )
                    ChartLegendItem(
                        color = CleansiaColors.successContainer,
                        label = stringResource(R.string.completed),
                        value = completedOrders.toString()
                    )
                }
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
