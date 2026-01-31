package cz.cleansia.partner.features.dashboard.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.KeyboardArrowUp
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.features.dashboard.viewmodels.AnalyticsPeriod
import cz.cleansia.partner.features.dashboard.viewmodels.AnalyticsUiState
import cz.cleansia.partner.features.dashboard.viewmodels.AnalyticsViewModel
import cz.cleansia.partner.features.dashboard.viewmodels.DayOfWeekEarnings
import cz.cleansia.partner.ui.components.GlassBackButton
import cz.cleansia.partner.ui.components.LoadingIndicator
import cz.cleansia.partner.ui.components.SparklineChart
import cz.cleansia.partner.ui.theme.CleansiaColors
import cz.cleansia.partner.core.utils.DateTimeUtils
import java.text.NumberFormat
import java.util.Currency
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AnalyticsDetailScreen(
    onNavigateBack: () -> Unit,
    viewModel: AnalyticsViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(uiState.error) {
        uiState.error?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    Box(modifier = Modifier.fillMaxSize()) {
        Scaffold(
            snackbarHost = { SnackbarHost(snackbarHostState) }
        ) { _ ->
            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .statusBarsPadding(),
                contentPadding = androidx.compose.foundation.layout.PaddingValues(
                    start = 16.dp,
                    end = 16.dp,
                    top = 60.dp,
                    bottom = 32.dp
                ),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                // Period selector chips
                item {
                    PeriodSelector(
                        selectedPeriod = uiState.selectedPeriod,
                        onPeriodSelected = { viewModel.selectPeriod(it) }
                    )
                }

                // Date range display
                item {
                    if (uiState.startDate != null && uiState.endDate != null) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.Center,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Icon(
                                imageVector = Icons.Default.CalendarMonth,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.padding(end = 8.dp)
                            )
                            Text(
                                text = "${DateTimeUtils.formatDate(uiState.startDate)} → ${DateTimeUtils.formatDate(uiState.endDate)}",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                }

                if (uiState.isLoading) {
                    item {
                        LoadingIndicator(
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(200.dp)
                        )
                    }
                } else {
                    // Main chart card
                    item {
                        ChartCard(uiState = uiState)
                    }

                    // Comparison card
                    item {
                        ComparisonCard(uiState = uiState)
                    }

                    // Stats row
                    item {
                        StatsRow(uiState = uiState)
                    }

                    // Day-of-week breakdown
                    if (uiState.dayOfWeekEarnings.any { it.totalAmount > 0 }) {
                        item {
                            DayOfWeekCard(
                                dayOfWeekEarnings = uiState.dayOfWeekEarnings,
                                currency = uiState.analytics?.currency ?: "CZK"
                            )
                        }
                    }
                }
            }
        }

        // Glass back button
        GlassBackButton(
            onNavigateBack = onNavigateBack,
            title = stringResource(R.string.analytics_title),
            modifier = Modifier
                .fillMaxWidth()
                .background(MaterialTheme.colorScheme.background)
        )
    }
}

@Composable
private fun PeriodSelector(
    selectedPeriod: AnalyticsPeriod,
    onPeriodSelected: (AnalyticsPeriod) -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        FilterChip(
            selected = selectedPeriod == AnalyticsPeriod.THIS_WEEK,
            onClick = { onPeriodSelected(AnalyticsPeriod.THIS_WEEK) },
            label = { Text(stringResource(R.string.this_week)) }
        )
        FilterChip(
            selected = selectedPeriod == AnalyticsPeriod.THIS_MONTH,
            onClick = { onPeriodSelected(AnalyticsPeriod.THIS_MONTH) },
            label = { Text(stringResource(R.string.this_month)) }
        )
        FilterChip(
            selected = selectedPeriod == AnalyticsPeriod.LAST_MONTH,
            onClick = { onPeriodSelected(AnalyticsPeriod.LAST_MONTH) },
            label = { Text(stringResource(R.string.last_month)) }
        )
    }
}

@Composable
private fun ChartCard(
    uiState: cz.cleansia.partner.features.dashboard.viewmodels.AnalyticsUiState
) {
    val analytics = uiState.analytics ?: return
    val chartData = analytics.dataPoints.map { it.amount.toFloat() }

    Card(
        modifier = Modifier.fillMaxWidth(),
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
                Text(
                    text = formatCurrencyAnalytics(analytics.totalEarnings, analytics.currency),
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onPrimaryContainer
                )
            }

            Spacer(modifier = Modifier.height(16.dp))

            if (chartData.isNotEmpty()) {
                SparklineChart(
                    data = chartData,
                    lineColor = MaterialTheme.colorScheme.onPrimaryContainer,
                    fillColor = MaterialTheme.colorScheme.onPrimaryContainer.copy(alpha = 0.15f),
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(180.dp)
                )
            }
        }
    }
}

@Composable
private fun ComparisonCard(
    uiState: cz.cleansia.partner.features.dashboard.viewmodels.AnalyticsUiState
) {
    val analytics = uiState.analytics ?: return
    val change = analytics.percentageChange
    val isPositive = change >= 0
    val changeIcon = when {
        change > 0 -> Icons.Default.KeyboardArrowUp
        change < 0 -> Icons.Default.KeyboardArrowDown
        else -> Icons.Default.Remove
    }
    val changeColor = when {
        change > 0 -> CleansiaColors.success
        change < 0 -> MaterialTheme.colorScheme.error
        else -> MaterialTheme.colorScheme.onSurfaceVariant
    }

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
                text = stringResource(R.string.period_comparison),
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )

            Spacer(modifier = Modifier.height(12.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = stringResource(R.string.current_period),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1
                    )
                    Text(
                        text = formatCurrencyAnalytics(analytics.totalEarnings, analytics.currency),
                        style = MaterialTheme.typography.titleSmall,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }

                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = stringResource(R.string.previous_period),
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1
                    )
                    Text(
                        text = formatCurrencyAnalytics(analytics.previousPeriodEarnings, analytics.currency),
                        style = MaterialTheme.typography.titleSmall,
                        fontWeight = FontWeight.Medium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }

                // Change badge
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier
                        .clip(RoundedCornerShape(12.dp))
                        .background(changeColor.copy(alpha = 0.1f))
                        .padding(horizontal = 8.dp, vertical = 4.dp)
                ) {
                    Icon(
                        imageVector = changeIcon,
                        contentDescription = null,
                        tint = changeColor,
                        modifier = Modifier.height(16.dp)
                    )
                    Text(
                        text = "${String.format("%.1f", kotlin.math.abs(change))}%",
                        style = MaterialTheme.typography.labelMedium,
                        fontWeight = FontWeight.Bold,
                        color = changeColor
                    )
                }
            }
        }
    }
}

@Composable
private fun StatsRow(
    uiState: AnalyticsUiState
) {
    val analytics = uiState.analytics ?: return

    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            StatMiniCard(
                label = stringResource(R.string.total),
                value = formatCurrencyAnalytics(analytics.totalEarnings, analytics.currency),
                modifier = Modifier.weight(1f)
            )
            StatMiniCard(
                label = stringResource(R.string.daily_average),
                value = formatCurrencyAnalytics(uiState.averageDaily, analytics.currency),
                modifier = Modifier.weight(1f)
            )
        }
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            StatMiniCard(
                label = stringResource(R.string.best_day),
                value = uiState.bestDay?.let {
                    formatCurrencyAnalytics(it.amount, analytics.currency)
                } ?: "-",
                subtitle = uiState.bestDay?.let { DateTimeUtils.formatDate(it.date) },
                modifier = Modifier.weight(1f)
            )
            StatMiniCard(
                label = stringResource(R.string.worst_day),
                value = uiState.worstDay?.let {
                    formatCurrencyAnalytics(it.amount, analytics.currency)
                } ?: "-",
                subtitle = uiState.worstDay?.let { DateTimeUtils.formatDate(it.date) },
                modifier = Modifier.weight(1f)
            )
        }
    }
}

@Composable
private fun StatMiniCard(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    subtitle: String? = null
) {
    Card(
        modifier = modifier,
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(10.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = value,
                style = MaterialTheme.typography.titleSmall,
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.onSurface,
                textAlign = TextAlign.Center,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
                fontSize = 13.sp
            )
            if (subtitle != null) {
                Text(
                    text = subtitle,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.primary,
                    textAlign = TextAlign.Center,
                    fontSize = 10.sp,
                    maxLines = 1
                )
            }
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

// ============================================================
// Day-of-Week Earnings Breakdown
// ============================================================

@Composable
private fun DayOfWeekCard(
    dayOfWeekEarnings: List<DayOfWeekEarnings>,
    currency: String
) {
    val dayLabels = listOf(
        stringResource(R.string.day_mon),
        stringResource(R.string.day_tue),
        stringResource(R.string.day_wed),
        stringResource(R.string.day_thu),
        stringResource(R.string.day_fri),
        stringResource(R.string.day_sat),
        stringResource(R.string.day_sun)
    )
    val maxAmount = dayOfWeekEarnings.maxOfOrNull { it.totalAmount } ?: 1.0

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(
                text = stringResource(R.string.earnings_by_day),
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onSurface
            )

            Spacer(modifier = Modifier.height(16.dp))

            dayOfWeekEarnings.forEachIndexed { index, dayData ->
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 3.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = dayLabels.getOrElse(index) { "" },
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.width(36.dp)
                    )

                    // Bar
                    val fraction = if (maxAmount > 0) (dayData.totalAmount / maxAmount).toFloat() else 0f
                    Box(
                        modifier = Modifier
                            .weight(1f)
                            .height(20.dp)
                            .clip(RoundedCornerShape(4.dp))
                            .background(MaterialTheme.colorScheme.surfaceVariant)
                    ) {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth(fraction.coerceIn(0f, 1f))
                                .height(20.dp)
                                .clip(RoundedCornerShape(4.dp))
                                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.7f + fraction * 0.3f))
                        )
                    }

                    Spacer(modifier = Modifier.width(8.dp))

                    Text(
                        text = formatCurrencyAnalytics(dayData.totalAmount, currency),
                        style = MaterialTheme.typography.labelSmall,
                        fontWeight = FontWeight.Medium,
                        color = MaterialTheme.colorScheme.onSurface,
                        modifier = Modifier.width(72.dp),
                        textAlign = TextAlign.End,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }
            }
        }
    }
}

private fun formatCurrencyAnalytics(amount: Double, currency: String = "CZK"): String {
    return try {
        val format = NumberFormat.getCurrencyInstance(Locale.getDefault())
        format.currency = Currency.getInstance(currency)
        format.format(amount)
    } catch (e: Exception) {
        "$currency ${String.format("%.0f", amount)}"
    }
}
