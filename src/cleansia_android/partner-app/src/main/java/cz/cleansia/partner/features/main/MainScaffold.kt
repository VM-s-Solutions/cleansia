package cz.cleansia.partner.features.main

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.Assignment
import androidx.compose.material.icons.outlined.AttachMoney
import androidx.compose.material.icons.outlined.Dashboard
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.dp
import androidx.navigation.NavBackStackEntry
import cz.cleansia.core.snackbar.SnackbarInsetScope
import cz.cleansia.partner.R
import cz.cleansia.partner.features.dashboard.DashboardScreen
import cz.cleansia.partner.features.invoices.InvoicesListScreen
import cz.cleansia.partner.features.orders.OrdersListScreen
import cz.cleansia.partner.features.profile.ProfileScreen
import cz.cleansia.partner.navigation.NavRoute
import kotlinx.coroutines.launch

/**
 * Tabs in the bottom-nav order — left to right. Each owns its own
 * scrollable Column inside the pager page; the floating bar overlaps the
 * bottom of the body so each screen reserves [BOTTOM_NAV_RESERVED_SPACE]
 * worth of trailing space to clear it.
 */
enum class MainTab(val labelRes: Int, val icon: ImageVector) {
    Dashboard(R.string.dashboard, Icons.Outlined.Dashboard),
    Orders(R.string.orders, Icons.AutoMirrored.Outlined.Assignment),
    Invoices(R.string.invoices, Icons.Outlined.AttachMoney),
    Profile(R.string.profile, Icons.Outlined.Person),
}

/**
 * Pager-driven bottom-nav shell mirroring customer-app's MainShell layout.
 *  - HorizontalPager so swipe gestures move between tabs (not just tap).
 *  - Tab body is edge-to-edge (no top inset wasted on a back row); each
 *    screen handles its own status-bar padding via a hero card.
 *  - Floating-island bar overlaps the pager bottom; pager pages add
 *    [contentBottomInset] worth of trailing Spacer so the last list item
 *    isn't hidden behind the pill.
 *  - SnackbarInsetScope lifts the global snackbar above the pill so error
 *    toasts don't get covered.
 */
@Composable
fun MainScaffold(
    onOpenOrderDetails: (String) -> Unit,
    onOpenInvoiceDetails: (String) -> Unit,
    onOpenProfileSection: (NavRoute) -> Unit,
    onOpenEarnings: () -> Unit,
    onOpenNotifications: () -> Unit,
    onSignedOut: () -> Unit,
    backStackEntry: NavBackStackEntry? = null,
) {
    val initialOrdinal = rememberSaveable { MainTab.Dashboard.ordinal }
    val pagerState = rememberPagerState(initialPage = initialOrdinal) { MainTab.values().size }
    val selected = MainTab.values()[pagerState.currentPage]
    val scope = rememberCoroutineScope()
    val selectTab: (MainTab) -> Unit = { target ->
        scope.launch { pagerState.animateScrollToPage(target.ordinal) }
    }

    // Observe pending tab switches written into Main's SavedStateHandle by
    // pushed routes (e.g. EarningsSummaryScreen → popBack + jump to
    // Invoices). This keeps cross-scaffold tab switches declarative — the
    // pushed screen writes a key, pops itself, and the scaffold reacts on
    // recompose by animating to the target tab. We null out the slot once
    // consumed so re-entering Main from elsewhere doesn't re-trigger.
    val savedHandle = backStackEntry?.savedStateHandle
    val pendingTabOrdinal by (
        savedHandle?.getStateFlow<Int?>(PENDING_TAB_KEY, null)
            ?: kotlinx.coroutines.flow.MutableStateFlow<Int?>(null)
    ).collectAsState(initial = null)
    LaunchedEffect(pendingTabOrdinal) {
        val ordinal = pendingTabOrdinal ?: return@LaunchedEffect
        val tab = MainTab.values().getOrNull(ordinal) ?: return@LaunchedEffect
        pagerState.animateScrollToPage(tab.ordinal)
        savedHandle?.set(PENDING_TAB_KEY, null)
    }

    // Pill bottom-bar height (64) + 12 vertical margin × 2 = ~88dp clearance.
    SnackbarInsetScope(88.dp)

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        HorizontalPager(
            state = pagerState,
            modifier = Modifier.fillMaxSize(),
        ) { page ->
            when (MainTab.values()[page]) {
                MainTab.Dashboard -> DashboardScreen(
                    onOrderClick = onOpenOrderDetails,
                    onOpenOrders = { selectTab(MainTab.Orders) },
                    onOpenEarnings = onOpenEarnings,
                    onOpenProfile = { selectTab(MainTab.Profile) },
                    onOpenDocuments = { onOpenProfileSection(NavRoute.ProfileDocuments) },
                    onOpenNotifications = onOpenNotifications,
                )
                MainTab.Orders -> OrdersListScreen(
                    onOrderClick = onOpenOrderDetails,
                )
                MainTab.Invoices -> InvoicesListScreen(
                    onInvoiceClick = onOpenInvoiceDetails,
                )
                MainTab.Profile -> ProfileScreen(
                    onNavigateBack = { selectTab(MainTab.Dashboard) },
                    onNavigateToPersonal = { onOpenProfileSection(NavRoute.ProfilePersonal()) },
                    onNavigateToAddress = { onOpenProfileSection(NavRoute.ProfileAddress()) },
                    onNavigateToIdentification = { onOpenProfileSection(NavRoute.ProfileIdentification()) },
                    onNavigateToBank = { onOpenProfileSection(NavRoute.ProfileBank()) },
                    onNavigateToEmergency = { onOpenProfileSection(NavRoute.ProfileEmergency) },
                    onNavigateToDocuments = { onOpenProfileSection(NavRoute.ProfileDocuments) },
                    onNavigateToLanguage = { onOpenProfileSection(NavRoute.PreferenceLanguage) },
                    onNavigateToTheme = { onOpenProfileSection(NavRoute.PreferenceTheme) },
                    onNavigateToDevices = { onOpenProfileSection(NavRoute.Devices) },
                    onSignedOut = onSignedOut,
                )
            }
        }

        // Floating bar sits above the gesture inset (so the pill clears the
        // system handle on devices with gesture nav).
        val navInsets = WindowInsets.navigationBars.asPaddingValues()
        FloatingIslandBottomBar(
            selected = selected,
            onSelect = selectTab,
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .padding(bottom = navInsets.calculateBottomPadding()),
        )
    }
}

/**
 * Trailing-space dp value tab pages should add to their scrollable Column
 * so the last item isn't hidden behind the floating bar. 64 (pill) + 24
 * vertical margin + 24 breathing room ≈ 112dp. Exported as a top-level so
 * any tab body can import it.
 */
val MainBottomNavInset: androidx.compose.ui.unit.Dp = 112.dp

/**
 * SavedStateHandle key on the Main backstack entry. Pushed routes can
 * write a [MainTab.ordinal] here just before popping back to Main —
 * MainScaffold observes the key and animates the pager to that tab. Lets
 * Earnings → "View invoices" land the cleaner on the Invoices tab with
 * the bottom-nav still visible, instead of stranding them inside a
 * standalone full-screen InvoicesListScreen with the scaffold hidden.
 */
const val PENDING_TAB_KEY = "main_pending_tab"
