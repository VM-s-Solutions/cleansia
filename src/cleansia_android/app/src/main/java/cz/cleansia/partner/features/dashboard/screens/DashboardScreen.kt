package cz.cleansia.partner.features.dashboard.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.features.dashboard.components.EarningsOverviewCard
import cz.cleansia.partner.features.dashboard.components.EmptyStateCard
import cz.cleansia.partner.features.dashboard.components.GreetingHero
import cz.cleansia.partner.features.dashboard.components.NextUpCard
import cz.cleansia.partner.features.dashboard.components.QuickStatsRow
import cz.cleansia.partner.features.dashboard.components.UpcomingOrderCard
import cz.cleansia.partner.features.dashboard.components.WorkingHoursCard
import cz.cleansia.partner.features.dashboard.viewmodels.DashboardUiState
import cz.cleansia.partner.features.dashboard.viewmodels.DashboardViewModel
import cz.cleansia.partner.features.dashboard.viewmodels.GreetingType
import cz.cleansia.partner.ui.components.CleansiaSnackbarHost
import cz.cleansia.partner.features.dashboard.components.DashboardSkeleton
import cz.cleansia.partner.ui.components.ErrorView
import cz.cleansia.partner.ui.components.SectionHeaderWithAction

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
    listState: LazyListState = rememberLazyListState(),
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

    Scaffold { paddingValues ->
        Box(modifier = Modifier.fillMaxSize()) {
        when {
            uiState.isLoading && uiState.stats == null -> {
                DashboardSkeleton(
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
                        onScrolled = onScrolled,
                        listState = listState
                    )
                }
            }
        }

        CleansiaSnackbarHost(hostState = snackbarHostState)
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

        // === Today's Working Hours ===
        item {
            WorkingHoursCard(
                workingInfo = uiState.todayWorkingInfo
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
