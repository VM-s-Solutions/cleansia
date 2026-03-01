package cz.cleansia.partner.features.dashboard.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CalendarMonth
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.ui.components.CleansiaSnackbarHost
import cz.cleansia.partner.features.dashboard.viewmodels.AnalyticsViewModel
import cz.cleansia.partner.features.dashboard.components.AnalyticsSkeleton
import cz.cleansia.partner.ui.components.GlassBackButton
import cz.cleansia.partner.core.utils.DateTimeUtils
import cz.cleansia.partner.features.dashboard.components.analytics.ChartCard
import cz.cleansia.partner.features.dashboard.components.analytics.ComparisonCard
import cz.cleansia.partner.features.dashboard.components.analytics.CompletionEfficiencyCard
import cz.cleansia.partner.features.dashboard.components.analytics.DayOfWeekCard
import cz.cleansia.partner.features.dashboard.components.analytics.MonthlyEarningsTrendCard
import cz.cleansia.partner.features.dashboard.components.analytics.OrderStatusCard
import cz.cleansia.partner.features.dashboard.components.analytics.PerformanceScoreCard
import cz.cleansia.partner.features.dashboard.components.analytics.PeriodSelector
import cz.cleansia.partner.features.dashboard.components.analytics.ScheduleUtilizationCard
import cz.cleansia.partner.features.dashboard.components.analytics.ServiceRevenueCard
import cz.cleansia.partner.features.dashboard.components.analytics.StatsRow

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
        Scaffold { _ ->
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
                        AnalyticsSkeleton(
                            modifier = Modifier
                                .fillMaxWidth()
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
                                currency = uiState.analytics?.currency ?: "EUR"
                            )
                        }
                    }

                    // Order status distribution
                    uiState.orderStatusDistribution?.let { data ->
                        item { OrderStatusCard(distribution = data) }
                    }

                    // Performance score
                    uiState.performanceScore?.let { data ->
                        item { PerformanceScoreCard(score = data) }
                    }

                    // Monthly earnings trend
                    uiState.monthlyEarningsTrend?.let { data ->
                        item {
                            MonthlyEarningsTrendCard(
                                trend = data,
                                currency = uiState.analytics?.currency ?: "EUR"
                            )
                        }
                    }

                    // Service revenue breakdown
                    uiState.serviceRevenueBreakdown?.let { data ->
                        item {
                            ServiceRevenueCard(
                                breakdown = data,
                                currency = uiState.analytics?.currency ?: "EUR"
                            )
                        }
                    }

                    // Schedule utilization
                    uiState.scheduleUtilization?.let { data ->
                        item { ScheduleUtilizationCard(utilization = data) }
                    }

                    // Completion efficiency
                    uiState.completionTimeEfficiency?.let { data ->
                        item { CompletionEfficiencyCard(efficiency = data) }
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

        CleansiaSnackbarHost(hostState = snackbarHostState)
    }
}
