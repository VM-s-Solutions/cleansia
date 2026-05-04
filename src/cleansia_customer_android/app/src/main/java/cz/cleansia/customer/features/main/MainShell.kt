package cz.cleansia.customer.features.main

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInHorizontally
import androidx.compose.animation.slideOutHorizontally
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CardGiftcard
import androidx.compose.material.icons.outlined.CleaningServices
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Receipt
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import kotlinx.coroutines.launch
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.catalog.CatalogRepositoryEntryPoint
import cz.cleansia.customer.core.data.AddressRepositoryEntryPoint
import cz.cleansia.customer.core.loyalty.LoyaltyRepositoryEntryPoint
import cz.cleansia.customer.core.orders.OrderRepositoryEntryPoint
import cz.cleansia.customer.core.referral.ReferralRepositoryEntryPoint
import cz.cleansia.customer.features.addresses.AddressManagerSheet
import cz.cleansia.customer.features.booking.BookingBottomSheet
import cz.cleansia.customer.features.home.HomeTab
import cz.cleansia.customer.features.orders.OrdersTab
import cz.cleansia.customer.features.profile.ProfileTab
import cz.cleansia.customer.features.profile.ProfileViewModel
import cz.cleansia.customer.features.rewards.RewardsTab
import cz.cleansia.customer.ui.theme.CleansiaTheme
import dagger.hilt.android.EntryPointAccessors

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
    // Wave 3 Phase R1 — set externally (NavHost) when the user taps "Book again"
    // on an order detail screen. When non-null, MainShell opens the booking
    // sheet on the next composition with this order id so the sheet can pre-fill.
    rebookOrderId: String? = null,
    onRebookConsumed: () -> Unit = {},
) {
    // rememberSaveable so the tab selection survives process death AND survives
    // navigation away + back (nav destinations disposing & recreating the composable).
    var selected by rememberSaveable { mutableStateOf(MainTab.Home) }
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
        selected = MainTab.Home
        rebookFromOrderId = incoming
        bookingSheetOpen = true
        onRebookConsumed()
    }

    val context = LocalContext.current

    // Fetch the signed-in user once per shell entry. The VM caches the result
    // on the singleton UserRepository so EditProfileScreen can read the same
    // snapshot without a second call.
    val profileVm: ProfileViewModel = hiltViewModel()
    val currentUser by profileVm.currentUser.collectAsState()
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
    val onboardingEntryPoint = remember {
        EntryPointAccessors.fromApplication(context, MainShellOnboardingEntryPoint::class.java)
    }
    var onboardingChecked by remember { mutableStateOf(false) }
    LaunchedEffect(currentUser?.id) {
        val user = currentUser ?: return@LaunchedEffect
        if (onboardingChecked) return@LaunchedEffect
        onboardingChecked = true

        // Force a server round-trip so the decision sees the authoritative
        // profile state — avoids races where the derived isProfileComplete
        // flow hasn't computed yet, or the cached snapshot is from a prior
        // session.
        val userRepo = onboardingEntryPoint.userRepository()
        userRepo.refreshCurrentUser()
        val refreshed = userRepo.currentUser.value ?: user
        val phoneOk = !refreshed.phoneNumber.isNullOrBlank()
        val nameOk = refreshed.firstName.isNotBlank() && refreshed.lastName.isNotBlank()
        val emailOk = refreshed.email.isNotBlank()
        val complete = phoneOk && nameOk && emailOk
        val seen = onboardingEntryPoint.appSettings().hasSeenOnboarding(refreshed.id)
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
    // Resolved via EntryPoint because the repo isn't owned by a VM at this scope,
    // and the call is a safe no-op for guests.
    val addressRepo = remember {
        EntryPointAccessors
            .fromApplication(context, AddressRepositoryEntryPoint::class.java)
            .addressRepository()
    }
    LaunchedEffect(Unit) {
        addressRepo.refreshFromServer()
    }

    // Warm the services/packages catalog so opening the booking sheet feels instant.
    // Gate on `loaded` to avoid re-fetches when the shell recomposes after nav.
    val catalogRepo = remember {
        EntryPointAccessors
            .fromApplication(context, CatalogRepositoryEntryPoint::class.java)
            .catalogRepository()
    }
    LaunchedEffect(Unit) {
        if (!catalogRepo.loaded.value) {
            catalogRepo.refresh()
        }
    }

    // Warm the orders cache so the Orders tab is instant on first tap. Gate on
    // `loaded` like the catalog — avoids re-fetching on every recomposition
    // after navigating back from a child screen.
    val orderRepo = remember {
        EntryPointAccessors
            .fromApplication(context, OrderRepositoryEntryPoint::class.java)
            .orderRepository()
    }
    LaunchedEffect(Unit) {
        if (!orderRepo.loaded.value) {
            orderRepo.refresh()
        }
    }

    // Warm the loyalty cache so the Rewards tab is instant on first tap. The
    // repo gates the network call on its own `loading` flag — and we additionally
    // gate on `loaded` here so navigating back to the shell from a child screen
    // doesn't re-fetch what's already cached.
    val loyaltyRepo = remember {
        EntryPointAccessors
            .fromApplication(context, LoyaltyRepositoryEntryPoint::class.java)
            .loyaltyRepository()
    }
    LaunchedEffect(Unit) {
        if (!loyaltyRepo.loaded.value) {
            loyaltyRepo.refresh()
        }
    }

    // Warm the referral cache (Loyalty Phase C) so the "Invite friends" card
    // on the Rewards tab is instant. Backend lazy-creates the user's code on
    // first call to `EnsureCodeForUserAsync`, so this prefetch also acts as
    // the issue trigger.
    val referralRepo = remember {
        EntryPointAccessors
            .fromApplication(context, ReferralRepositoryEntryPoint::class.java)
            .referralRepository()
    }
    LaunchedEffect(Unit) {
        if (!referralRepo.loaded.value) {
            referralRepo.refresh()
        }
    }

    val openBooking = { bookingSheetOpen = true }

    // Lift the global snackbar above the custom bottom bar on every tab.
    // Bar is ~76dp (60dp + FAB overhang); +12dp gives a visible gap.
    cz.cleansia.customer.ui.snackbar.SnackbarInsetScope(88.dp)

    Box(modifier = Modifier.fillMaxSize()) {
        Scaffold(
            bottomBar = {
                CustomBottomBar(
                    selected = selected,
                    onSelect = { selected = it },
                    onBookClick = openBooking,
                )
            },
        ) { padding ->
            AnimatedContent(
                targetState = selected,
                transitionSpec = {
                    val forward = targetState.ordinal > initialState.ordinal
                    val slideDistance = if (forward) 1 else -1
                    (slideInHorizontally(animationSpec = tween(250)) { it * slideDistance } +
                        fadeIn(animationSpec = tween(250))) togetherWith
                        (slideOutHorizontally(animationSpec = tween(250)) { -it * slideDistance } +
                            fadeOut(animationSpec = tween(250)))
                },
                label = "tab-transition",
            ) { currentTab ->
                when (currentTab) {
                    MainTab.Home -> HomeTab(
                        modifier = Modifier.padding(padding),
                        onBookCleaning = openBooking,
                        onViewAllServices = openBooking,
                        onOpenAddressManager = { addressSheetOpen = true },
                        onOrderClick = onOrderClick,
                        onSeeAllOrders = { selected = MainTab.Orders },
                        onSubscribePlus = onSubscribePlus,
                        onOpenReferral = { selected = MainTab.Rewards },
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
                    )
                    MainTab.Orders -> OrdersTab(
                        modifier = Modifier.padding(padding),
                        onOrderClick = onOrderClick,
                        onBookCleaning = openBooking,
                    )
                    MainTab.Rewards -> RewardsTab(
                        modifier = Modifier.padding(padding),
                        onOpenActivity = onOpenRewardsActivity,
                    )
                    MainTab.Profile -> ProfileTab(
                        modifier = Modifier.padding(padding),
                        user = currentUser,
                        onLogout = onLogout,
                        onRowClick = onProfileRow,
                    )
                }
            }
        }

        // Sheet rendered as overlay — its empty space passes touches to the Scaffold below
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
 * Custom 5-slot bottom bar:
 * Home · Orders · [Book FAB] · Offers · Profile
 * Center button is elevated with shadow, overlaps top of bar.
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
            .padding(bottom = 6.dp), // Sit just above gesture bar
    ) {
        // Bar background
        Column {
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant, thickness = 1.dp)
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(MaterialTheme.colorScheme.surface)
                    .padding(top = 6.dp, bottom = 4.dp),
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
        }

        // Elevated Book FAB — slightly raised above bar
        Box(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .offset(y = (-18).dp),
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
