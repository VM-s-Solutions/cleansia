package cz.cleansia.customer.features.main

import cz.cleansia.core.snackbar.SnackbarInsetScope

import androidx.compose.animation.core.animateDpAsState
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CardGiftcard
import androidx.compose.material.icons.outlined.CleaningServices
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Receipt
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import kotlinx.coroutines.launch
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.features.addresses.AddressManagerSheet
import cz.cleansia.customer.features.booking.BookingBottomSheet
import cz.cleansia.customer.features.home.HomeTab
import cz.cleansia.customer.features.orders.OrdersTab
import cz.cleansia.customer.features.profile.ProfileTab
import cz.cleansia.customer.features.profile.ProfileViewModel
import cz.cleansia.customer.features.rewards.RewardsTab
import cz.cleansia.customer.ui.theme.CleansiaTheme

enum class MainTab { Home, Orders, Rewards, Profile }

@Composable
fun MainShell(
    onOrderClick: (orderId: String) -> Unit = {},
    onLogout: () -> Unit = {},
    onProfileRow: (key: String) -> Unit = {},
    onBookingComplete: (confirmationCode: String, orderId: String) -> Unit = { _, _ -> },
    onNavigateToEditProfile: () -> Unit = {},
    onNavigateToOnboarding: () -> Unit = {},
    onOpenRewardsActivity: () -> Unit = {},
    /** Tap on the Home upsell carousel's Plus card. Routes to Subscribe Plus. */
    onSubscribePlus: () -> Unit = {},
    /** Tap on "Set up recurring" affordance from Home (carousel slide or empty section). */
    onSetupRecurring: () -> Unit = {},
    /** Tap on "Manage" / open a specific recurring schedule from Home. */
    onManageRecurring: () -> Unit = {},
    /** Tap on a notifications-inbox row with a deep link. Receives a typed `Routes.X` value. */
    onOpenNotificationRoute: (Any) -> Unit = {},
    // Wave 3 Phase R1 — set externally (NavHost) when the user taps "Book again"
    // on an order detail screen. When non-null, MainShell opens the booking
    // sheet on the next composition with this order id so the sheet can pre-fill.
    rebookOrderId: String? = null,
    onRebookConsumed: () -> Unit = {},
    shellViewModel: MainShellViewModel = hiltViewModel(),
) {
    // Pager-driven tab state — `selected` is derived from `pagerState.currentPage`
    // and changing it animates the pager. rememberSaveable on the initial-page
    // index so the tab survives process death + nav-back recompositions.
    val initialTabOrdinal = rememberSaveable { MainTab.Home.ordinal }
    val pagerState = rememberPagerState(initialPage = initialTabOrdinal) { MainTab.entries.size }
    val selected = MainTab.entries[pagerState.currentPage]
    val tabScope = rememberCoroutineScope()
    val selectTab: (MainTab) -> Unit = { target ->
        tabScope.launch { pagerState.animateScrollToPage(target.ordinal) }
    }
    var bookingSheetOpen by remember { mutableStateOf(false) }
    // Tracks the order id whose data should pre-fill the booking sheet. Set by
    // the rebookOrderId LaunchedEffect (driven by the nav arg) and by
    // openBookingRebook (in-app callbacks). Cleared on dismiss / complete so a
    // subsequent fresh booking doesn't re-pre-fill.
    var rebookFromOrderId by remember { mutableStateOf<String?>(null) }
    // Set when a popular-package card on home is tapped. The booking sheet
    // reads it once on first open to seed selectedPackageIds, then we clear
    // it on dismiss/complete so a fresh booking from the FAB doesn't carry
    // a stale package over.
    var prefillPackageId by remember { mutableStateOf<String?>(null) }
    // Used by onComplete to fire-and-forget a refresh of the orders cache
    // after a successful booking (so the new order shows on the Orders tab).
    val scope = rememberCoroutineScope()
    var addressSheetOpen by remember { mutableStateOf(false) }
    // Set when we dismiss the booking sheet to send the user to Edit Profile
    // for a missing phone. When the profile save completes and they navigate
    // back, we re-open the sheet so they don't have to rebuild the booking.
    //
    // rememberSaveable so the flag survives MainShell composable disposal —
    // CleansiaNavHost destroys the Home composable when navigating to
    // EditProfile, so a plain `remember` would lose the flag and the sheet
    // wouldn't reopen on return.
    var reopenBookingAfterProfile by rememberSaveable { mutableStateOf(false) }

    // External rebook trigger — NavHost passes a one-shot orderId via the
    // rebookOrderId param. We mirror it into local state, open the sheet, and
    // notify NavHost so it can clear its own copy (preventing re-trigger on
    // recomposition / configuration change).
    LaunchedEffect(rebookOrderId) {
        val incoming = rebookOrderId ?: return@LaunchedEffect
        pagerState.scrollToPage(MainTab.Home.ordinal)
        rebookFromOrderId = incoming
        bookingSheetOpen = true
        onRebookConsumed()
    }

    // Fetch the signed-in user once per shell entry. The VM caches the result
    // on the singleton UserRepository so EditProfileScreen can read the same
    // snapshot without a second call.
    val profileVm: ProfileViewModel = hiltViewModel()
    val currentUser by profileVm.currentUser.collectAsStateWithLifecycle()
    LaunchedEffect(Unit) {
        if (currentUser == null) profileVm.refresh()
    }

    // First time the user lands on MainShell with an incomplete profile — and
    // they haven't dismissed the onboarding before — drop them into the
    // onboarding flow. One-shot per shell composition; after they save or
    // skip, markOnboardingSeen() makes the gate pass on next app launch.
    //
    // We force a fresh profile fetch BEFORE deciding so we don't trust a
    // stale cached `isProfileComplete` — that was the source of an earlier
    // bug where new signups skipped onboarding because the derived flow
    // hadn't propagated yet, and the user later got silent order failures
    // on a missing phone.
    var onboardingChecked by remember { mutableStateOf(false) }
    LaunchedEffect(currentUser?.id) {
        val user = currentUser ?: return@LaunchedEffect
        if (onboardingChecked) return@LaunchedEffect
        onboardingChecked = true

        // Force a server round-trip so the decision sees the authoritative
        // profile state — avoids races where the derived isProfileComplete
        // flow hasn't computed yet, or the cached snapshot is from a prior
        // session.
        val userRepo = shellViewModel.userRepository
        userRepo.refreshCurrentUser()
        val refreshed = userRepo.currentUser.value ?: user
        val phoneOk = !refreshed.phoneNumber.isNullOrBlank()
        val nameOk = refreshed.firstName.isNotBlank() && refreshed.lastName.isNotBlank()
        val emailOk = refreshed.email.isNotBlank()
        val complete = phoneOk && nameOk && emailOk
        val seen = shellViewModel.appSettings.hasSeenOnboarding(refreshed.id)
        android.util.Log.d(
            "OnboardingGate",
            "decision: phoneOk=$phoneOk nameOk=$nameOk emailOk=$emailOk complete=$complete hasSeen=$seen",
        )
        if (!complete && !seen) {
            onNavigateToOnboarding()
        }
    }

    // When the user returns from Edit Profile after filling a missing phone,
    // auto-reopen the booking sheet so they don't have to rebuild their cart.
    // Keyed on the phone value so this fires exactly when it transitions to set.
    val currentPhone = currentUser?.phoneNumber
    LaunchedEffect(currentPhone) {
        if (reopenBookingAfterProfile && !currentPhone.isNullOrBlank()) {
            bookingSheetOpen = true
            reopenBookingAfterProfile = false
        }
    }

    // Warm the address cache from the server in parallel with the profile fetch.
    // The VM surfaces a snackbar on HTTP failure; guests and connectivity
    // failures stay silent. Safe no-op for guests.
    LaunchedEffect(Unit) {
        shellViewModel.refreshAddresses()
    }

    // Warm the services/packages catalog so opening the booking sheet feels instant.
    // Gate on `loaded` to avoid re-fetches when the shell recomposes after nav.
    val catalogRepo = shellViewModel.catalogRepository
    LaunchedEffect(Unit) {
        if (!catalogRepo.loaded.value) {
            shellViewModel.refreshCatalog()
        }
    }

    // Warm the orders cache so the Orders tab is instant on first tap. Gate on
    // `loaded` like the catalog — avoids re-fetching on every recomposition
    // after navigating back from a child screen.
    val orderRepo = shellViewModel.orderRepository
    LaunchedEffect(Unit) {
        if (!orderRepo.loaded.value) {
            orderRepo.refresh()
        }
    }

    // Warm the loyalty cache so the Rewards tab is instant on first tap. The
    // repo gates the network call on its own `loading` flag — and we additionally
    // gate on `loaded` here so navigating back to the shell from a child screen
    // doesn't re-fetch what's already cached.
    val loyaltyRepo = shellViewModel.loyaltyRepository
    LaunchedEffect(Unit) {
        if (!loyaltyRepo.loaded.value) {
            loyaltyRepo.refresh()
        }
    }

    // Warm the referral cache (Loyalty Phase C) so the "Invite friends" card
    // on the Rewards tab is instant. Backend lazy-creates the user's code on
    // first call to `EnsureCodeForUserAsync`, so this prefetch also acts as
    // the issue trigger.
    val referralRepo = shellViewModel.referralRepository
    LaunchedEffect(Unit) {
        if (!referralRepo.loaded.value) {
            referralRepo.refresh()
        }
    }

    val openBooking = { bookingSheetOpen = true }

    // Lift the global snackbar above the custom bottom bar on every tab.
    // Bar is ~76dp (60dp + FAB overhang); +12dp gives a visible gap.
    cz.cleansia.core.snackbar.SnackbarInsetScope(88.dp)

    Box(modifier = Modifier.fillMaxSize()) {
        // Pager fills the full screen; tabs paint their own background
        // (Slate50) edge-to-edge so the area "behind" the floating pill
        // is one continuous page background — making the pill genuinely
        // float on the page, not sit on a separate band.
        //
        // Bottom inset for the pill is handled by `MainTab.contentBottomInset`
        // which each tab adds as a trailing Spacer in its scroll column, so
        // the last list item isn't hidden behind the pill.
        val navInsets = WindowInsets.navigationBars.asPaddingValues()

        HorizontalPager(
            state = pagerState,
            modifier = Modifier.fillMaxSize(),
        ) { page ->
            when (MainTab.entries[page]) {
                MainTab.Home -> HomeTab(
                    onBookCleaning = openBooking,
                    onViewAllServices = openBooking,
                    onOpenAddressManager = { addressSheetOpen = true },
                    onOrderClick = onOrderClick,
                    onSeeAllOrders = { selectTab(MainTab.Orders) },
                    onSubscribePlus = onSubscribePlus,
                    onOpenReferral = { selectTab(MainTab.Rewards) },
                    onBookPackage = { packageId ->
                        prefillPackageId = packageId
                        bookingSheetOpen = true
                    },
                    onRebookOrder = { orderId ->
                        rebookFromOrderId = orderId
                        bookingSheetOpen = true
                    },
                    onSetupRecurring = onSetupRecurring,
                    onManageRecurring = onManageRecurring,
                    onOpenNotificationRoute = onOpenNotificationRoute,
                )
                MainTab.Orders -> OrdersTab(
                    onOrderClick = onOrderClick,
                    onBookCleaning = openBooking,
                )
                MainTab.Rewards -> RewardsTab(
                    onOpenActivity = onOpenRewardsActivity,
                )
                MainTab.Profile -> ProfileTab(
                    user = currentUser,
                    onLogout = onLogout,
                    onRowClick = onProfileRow,
                )
            }
        }

        // Floating island bottom bar — sits above the pager. Both the
        // pager background and the area below the pill are painted with
        // `colorScheme.background` (root Surface in MainActivity), so the
        // pill floats on a single continuous color. Only the gesture-bar
        // inset is reserved here so the pill clears the system handle.
        Box(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .padding(bottom = navInsets.calculateBottomPadding()),
        ) {
            CustomBottomBar(
                selected = selected,
                onSelect = selectTab,
                onBookClick = openBooking,
            )
        }

        // Sheet rendered as overlay — its empty space passes touches to the pager below
        BookingBottomSheet(
            visible = bookingSheetOpen,
            rebookFromOrderId = rebookFromOrderId,
            prefillPackageId = prefillPackageId,
            onDismiss = {
                bookingSheetOpen = false
                rebookFromOrderId = null
                prefillPackageId = null
            },
            onComplete = { confirmationCode, orderId ->
                bookingSheetOpen = false
                rebookFromOrderId = null
                prefillPackageId = null
                // Refresh the orders cache the moment a booking is confirmed
                // so the new order shows up on the Orders tab without the
                // user needing to pull-to-refresh. Fire-and-forget — the
                // BookingSuccess screen is what they're navigating to first.
                scope.launch { orderRepo.refresh() }
                onBookingComplete(confirmationCode, orderId)
            },
            onNavigateToEditProfile = {
                bookingSheetOpen = false
                // Keep `rebookFromOrderId` set — when the user returns from Edit
                // Profile, reopenBookingAfterProfile flips bookingSheetOpen back
                // to true and the sheet re-pre-fills from the same order id.
                // The lastRebookedFrom guard inside the sheet keeps it idempotent.
                reopenBookingAfterProfile = true
                onNavigateToEditProfile()
            },
        )

        // Address manager, same bottom-sheet behavior as the booking sheet.
        AddressManagerSheet(
            visible = addressSheetOpen,
            onDismiss = { addressSheetOpen = false },
            onAddressSelected = { addressSheetOpen = false },
        )
    }
}

/**
 * Floating island bottom bar — Wolt/Bolt style. Pill-shaped surface that
 * floats with horizontal margin from the screen edges and clear of the
 * gesture area. 5 slots: Home · Orders · [Book FAB] · Rewards · Profile.
 * The Book FAB sits centered, half-overlapping the top of the pill so it
 * reads as the primary action.
 */
@Composable
private fun CustomBottomBar(
    selected: MainTab,
    onSelect: (MainTab) -> Unit,
    onBookClick: () -> Unit,
) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 12.dp),
    ) {
        // Floating pill — rounded, elevated, sits inset from the screen edges.
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(64.dp)
                .clip(RoundedCornerShape(32.dp))
                .background(MaterialTheme.colorScheme.surface)
                .border(
                    width = 1.dp,
                    color = MaterialTheme.colorScheme.outlineVariant,
                    shape = RoundedCornerShape(32.dp),
                )
                .padding(horizontal = 8.dp),
            horizontalArrangement = Arrangement.SpaceEvenly,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            NavSlot(MainTab.Home, Icons.Outlined.Home, R.string.nav_home, selected, onSelect)
            NavSlot(MainTab.Orders, Icons.Outlined.Receipt, R.string.nav_orders, selected, onSelect)
            // Center spacer for FAB
            Spacer(Modifier.size(72.dp))
            NavSlot(MainTab.Rewards, Icons.Outlined.CardGiftcard, R.string.nav_rewards, selected, onSelect)
            NavSlot(MainTab.Profile, Icons.Outlined.Person, R.string.nav_profile, selected, onSelect)
        }

        // Elevated Book FAB — half-overlapping the top of the pill
        Box(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .offset(y = (-12).dp),
        ) {
            BookFab(onBookClick)
        }
    }
}

@Composable
private fun NavSlot(
    tab: MainTab,
    icon: ImageVector,
    labelRes: Int,
    currentSelected: MainTab,
    onSelect: (MainTab) -> Unit,
) {
    val isSelected = tab == currentSelected
    val color = if (isSelected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant
    // Animate dot width on selection — 0 when unselected, 20dp pill when selected
    val dotWidth by animateDpAsState(
        targetValue = if (isSelected) 20.dp else 0.dp,
        animationSpec = androidx.compose.animation.core.tween(durationMillis = 200),
        label = "nav-dot",
    )
    Column(
        modifier = Modifier
            .clickable(
                interactionSource = remember { androidx.compose.foundation.interaction.MutableInteractionSource() },
                indication = null,
            ) { onSelect(tab) }
            .padding(horizontal = 8.dp, vertical = 6.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Icon(icon, contentDescription = null, tint = color, modifier = Modifier.size(24.dp))
        Spacer(Modifier.height(2.dp))
        Text(
            stringResource(labelRes),
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = if (isSelected) FontWeight.SemiBold else FontWeight.Normal),
            color = color,
        )
        Spacer(Modifier.height(3.dp))
        // Animated active indicator — pill that grows in when selected
        Box(
            modifier = Modifier
                .size(width = dotWidth, height = 3.dp)
                .clip(androidx.compose.foundation.shape.RoundedCornerShape(999.dp))
                .background(MaterialTheme.colorScheme.primary),
        )
    }
}

@Composable
private fun BookFab(onClick: () -> Unit) {
    Box(
        modifier = Modifier
            .size(74.dp)
            .clip(CircleShape)
            .background(MaterialTheme.colorScheme.primary)
            .border(
                width = 4.dp,
                color = MaterialTheme.colorScheme.background,
                shape = CircleShape,
            )
            .clickable(onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            Icons.Outlined.CleaningServices,
            contentDescription = stringResource(R.string.nav_book),
            tint = MaterialTheme.colorScheme.onPrimary,
            modifier = Modifier.size(34.dp),
        )
    }
}

@Preview(widthDp = 390, heightDp = 844)
@Composable
private fun MainShellPreview() {
    CleansiaTheme { MainShell() }
}
