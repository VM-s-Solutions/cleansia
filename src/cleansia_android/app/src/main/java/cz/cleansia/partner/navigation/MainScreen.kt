package cz.cleansia.partner.navigation

import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Dashboard
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.Receipt
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.outlined.Dashboard
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.Receipt
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.zIndex
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.partner.R
import cz.cleansia.partner.features.dashboard.screens.DashboardScreen
import cz.cleansia.partner.features.invoices.screens.InvoicesScreen
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.features.orders.screens.OrdersScreen
import cz.cleansia.partner.features.orders.viewmodels.OrderTab
import cz.cleansia.partner.features.search.GlobalSearchViewModel
import cz.cleansia.partner.ui.components.FloatingNavItem
import cz.cleansia.partner.ui.components.GlobalSearchOverlay
import cz.cleansia.partner.ui.components.FloatingBottomNavigation
import kotlinx.coroutines.launch

data class BottomNavItem(
    val route: NavRoute,
    val titleResId: Int,
    val selectedIcon: ImageVector,
    val unselectedIcon: ImageVector
)

val bottomNavItems = listOf(
    BottomNavItem(
        route = NavRoute.Dashboard,
        titleResId = R.string.dashboard,
        selectedIcon = Icons.Filled.Dashboard,
        unselectedIcon = Icons.Outlined.Dashboard
    ),
    BottomNavItem(
        route = NavRoute.Orders,
        titleResId = R.string.orders,
        selectedIcon = Icons.Filled.Description,
        unselectedIcon = Icons.Outlined.Description
    ),
    BottomNavItem(
        route = NavRoute.Invoices,
        titleResId = R.string.invoices,
        selectedIcon = Icons.Filled.Receipt,
        unselectedIcon = Icons.Outlined.Receipt
    )
)

@Composable
fun MainScreen(
    userInitials: String,
    onNavigateToOrderDetails: (String) -> Unit,
    onNavigateToInvoiceDetails: (String) -> Unit,
    onNavigateToAnalytics: () -> Unit = {},
    onNavigateToAccountHub: () -> Unit,
    searchViewModel: GlobalSearchViewModel = hiltViewModel()
) {
    val pagerState = rememberPagerState(pageCount = { bottomNavItems.size })
    val coroutineScope = rememberCoroutineScope()
    val searchState by searchViewModel.state.collectAsState()
    val pageScrollStates = remember { mutableStateMapOf(0 to false, 1 to false, 2 to false) }
    val isContentScrolled = pageScrollStates[pagerState.currentPage] ?: false

    // State for passing tab/filter info to OrdersScreen when navigating from dashboard
    var ordersInitialTab by remember { mutableStateOf<OrderTab?>(null) }
    var ordersInitialStatusFilter by remember { mutableStateOf<OrderStatus?>(null) }

    // Derive page title from current pager position
    val pageTitles = bottomNavItems.map { it.titleResId }

    // Header background - instant, no animation
    val headerBackgroundColor = if (isContentScrolled)
        MaterialTheme.colorScheme.surface
    else
        Color.Transparent
    val headerShape = RoundedCornerShape(bottomStart = 20.dp, bottomEnd = 20.dp)

    Box(modifier = Modifier.fillMaxSize()) {
        // Top bar overlay - floating islands
        Row(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .zIndex(1f)
                .fillMaxWidth()
                .clip(headerShape)
                .background(headerBackgroundColor)
                .statusBarsPadding()
                .padding(horizontal = 16.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            // Profile avatar (left island)
            Surface(
                modifier = Modifier
                    .border(
                        width = 0.15.dp,
                        color = MaterialTheme.colorScheme.outline.copy(alpha = 1f),
                        shape = CircleShape
                    )
                    .shadow(
                        elevation = 3.5.dp,
                        shape = CircleShape,
                        ambientColor = Color.Black.copy(alpha = 0.15f),
                        spotColor = Color.Black.copy(alpha = 0.25f)
                    ),
                shape = CircleShape,
                color = MaterialTheme.colorScheme.surface,
                onClick = onNavigateToAccountHub
            ) {
                Box(
                    modifier = Modifier
                        .size(40.dp)
                        .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = userInitials,
                        style = MaterialTheme.typography.labelMedium,
                        fontWeight = FontWeight.Bold,
                        fontSize = 14.sp,
                        color = MaterialTheme.colorScheme.onPrimaryContainer
                    )
                }
            }

            // Page title (center)
            Box(
                modifier = Modifier.weight(1f),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = stringResource(pageTitles[pagerState.currentPage]),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface
                )
            }

            // Search button (right island)
            Surface(
                modifier = Modifier
                    .border(
                        width = 0.15.dp,
                        color = MaterialTheme.colorScheme.outline.copy(alpha = 1f),
                        shape = CircleShape
                    )
                    .shadow(
                        elevation = 3.5.dp,
                        shape = CircleShape,
                        ambientColor = Color.Black.copy(alpha = 0.15f),
                        spotColor = Color.Black.copy(alpha = 0.25f)
                    ),
                shape = CircleShape,
                color = MaterialTheme.colorScheme.surface,
                onClick = { searchViewModel.setActive(true) }
            ) {
                Box(
                    modifier = Modifier.size(40.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = Icons.Default.Search,
                        contentDescription = stringResource(R.string.search),
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(20.dp)
                    )
                }
            }
        }

        // Main pager content
        HorizontalPager(
            state = pagerState,
            modifier = Modifier.fillMaxSize(),
            userScrollEnabled = true
        ) { page ->
            when (page) {
                0 -> DashboardScreen(
                    onNavigateToOrderDetails = onNavigateToOrderDetails,
                    onNavigateToOrders = {
                        ordersInitialTab = null
                        ordersInitialStatusFilter = null
                        coroutineScope.launch { pagerState.animateScrollToPage(1) }
                    },
                    onNavigateToAvailableOrders = {
                        ordersInitialTab = OrderTab.AVAILABLE
                        ordersInitialStatusFilter = null
                        coroutineScope.launch { pagerState.animateScrollToPage(1) }
                    },
                    onNavigateToActiveOrders = {
                        ordersInitialTab = OrderTab.MY_ORDERS
                        ordersInitialStatusFilter = null
                        coroutineScope.launch { pagerState.animateScrollToPage(1) }
                    },
                    onNavigateToCompletedOrders = {
                        ordersInitialTab = OrderTab.MY_ORDERS
                        ordersInitialStatusFilter = OrderStatus.COMPLETED
                        coroutineScope.launch { pagerState.animateScrollToPage(1) }
                    },
                    onNavigateToInvoices = {
                        coroutineScope.launch { pagerState.animateScrollToPage(2) }
                    },
                    onNavigateToAnalytics = onNavigateToAnalytics,
                    onScrolled = { scrolled -> pageScrollStates[0] = scrolled }
                )
                1 -> OrdersScreen(
                    onNavigateToOrderDetails = onNavigateToOrderDetails,
                    onScrolled = { scrolled -> pageScrollStates[1] = scrolled },
                    initialTab = ordersInitialTab,
                    initialStatusFilter = ordersInitialStatusFilter
                )
                2 -> InvoicesScreen(
                    onNavigateToInvoiceDetails = onNavigateToInvoiceDetails,
                    onScrolled = { scrolled -> pageScrollStates[2] = scrolled }
                )
            }
        }

        // Bottom navigation
        FloatingBottomNavigation(
            items = bottomNavItems.mapIndexed { index, item ->
                FloatingNavItem(
                    titleResId = item.titleResId,
                    selectedIcon = item.selectedIcon,
                    unselectedIcon = item.unselectedIcon,
                    isSelected = pagerState.currentPage == index,
                    onClick = {
                        coroutineScope.launch { pagerState.animateScrollToPage(index) }
                    }
                )
            },
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
        )

        // Global search overlay
        if (searchState.isActive) {
            GlobalSearchOverlay(
                state = searchState,
                onQueryChange = { searchViewModel.updateQuery(it) },
                onDismiss = { searchViewModel.setActive(false) },
                onOrderClick = onNavigateToOrderDetails,
                onInvoiceClick = onNavigateToInvoiceDetails,
                modifier = Modifier
                    .fillMaxSize()
                    .zIndex(2f)
            )
        }
    }
}
