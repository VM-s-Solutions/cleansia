package cz.cleansia.customer.features.home

import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.animateFloat
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.collectIsDraggedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.requiredSize
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Remove
import androidx.compose.material.icons.automirrored.outlined.ArrowForward
import androidx.compose.material.icons.automirrored.outlined.ArrowForwardIos
import androidx.compose.material.icons.outlined.AccountBalanceWallet
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material.icons.outlined.Bolt
import androidx.compose.material.icons.outlined.CardGiftcard
import androidx.compose.material.icons.outlined.CleaningServices
import androidx.compose.material.icons.outlined.EmojiEvents
import androidx.compose.material.icons.outlined.EventAvailable
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.KeyboardArrowDown
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material.icons.outlined.NotificationsNone
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material.icons.outlined.Repeat
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.minimumInteractiveComponentSize
import androidx.compose.material3.ripple
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.material3.pulltorefresh.rememberPullToRefreshState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.CustomAccessibilityAction
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.customActions
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.ui.components.CleansiaChip
import cz.cleansia.core.ui.components.SudsRefreshIndicator
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.customer.core.market.MarketListItem
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.core.market.countryId
import cz.cleansia.customer.core.market.formattedReferralCredit
import cz.cleansia.customer.core.market.offersAChoice
import cz.cleansia.customer.core.market.selectedOrNull
import cz.cleansia.customer.features.booking.localizedName
import cz.cleansia.customer.features.recurring.ScheduleStatus
import cz.cleansia.core.ui.theme.Poppins
import cz.cleansia.customer.R
import cz.cleansia.customer.core.booking.PropertySize
import cz.cleansia.customer.core.loyalty.CreditBalanceDto
import cz.cleansia.customer.core.loyalty.LoyaltyAccountDto
import cz.cleansia.customer.core.loyalty.LoyaltyTier
import cz.cleansia.customer.core.memberships.ExpressWaiverStatus
import cz.cleansia.customer.core.memberships.benefitsPaused
import cz.cleansia.customer.core.memberships.resolveExpressWaiver
import cz.cleansia.customer.features.booking.BOOKING_SLOT_INTERVAL_MINUTES
import cz.cleansia.customer.features.booking.BookingPricing
import cz.cleansia.customer.features.booking.FIRST_WINDOW_HOUR
import cz.cleansia.customer.features.booking.LAST_WINDOW_HOUR
import cz.cleansia.customer.features.booking.cancellationPolicyFor
import cz.cleansia.customer.core.orders.OrderListItemDto
import cz.cleansia.customer.features.booking.localizedName
import cz.cleansia.customer.features.orders.OrderStatus
import cz.cleansia.customer.features.orders.orderStatusFromValue
import cz.cleansia.customer.features.orders.orderStatusLabelRes
import cz.cleansia.customer.ui.format.orderStatusColor
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.customer.ui.theme.SuccessText
import cz.cleansia.customer.ui.theme.WarningStar
import cz.cleansia.customer.features.main.MainShellBottomClearance
import cz.cleansia.customer.ui.components.statusBarFade
import kotlin.math.roundToInt
import kotlinx.coroutines.launch

/* ── Presentation models ── */

private data class PastCleaner(val id: String, val name: String, val rating: Float, val jobs: Int)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HomeTab(
    modifier: Modifier = Modifier,
    onBookCleaning: () -> Unit = {},
    onViewAllServices: () -> Unit = {},
    onOpenAddressManager: () -> Unit = {},
    onOrderClick: (String) -> Unit = {},
    onSeeAllOrders: () -> Unit = {},
    onSubscribePlus: () -> Unit = {},
    /** Tap on the header's market chip. Opens the Market preference screen (ADR-0058 D6). */
    onOpenMarket: () -> Unit = {},
    onOpenReferral: () -> Unit = {},
    /** Tap on a popular-package card. Opens booking sheet pre-filled with the package. */
    onBookPackage: (String) -> Unit = {},
    /** Tap on the "Order again" quick-action card. Opens booking sheet pre-filled from the order. */
    onRebookOrder: (String) -> Unit = {},
    /** Tap on the "Set up recurring" affordance. Routes to the create wizard. */
    onSetupRecurring: () -> Unit = {},
    /** Tap on a recurring-schedule row. Routes to the management screen. */
    onManageRecurring: () -> Unit = {},
    /** Tap on an inbox row with a deep link. Receives a typed `Routes.X` value. */
    onOpenNotificationRoute: (Any) -> Unit = {},
    /** "See my price" on the carousel's quick-size slide. Opens booking with the size already set. */
    onBookSize: (rooms: Int, bathrooms: Int) -> Unit = { _, _ -> },
    viewModel: HomeTabViewModel = androidx.hilt.navigation.compose.hiltViewModel(),
) {
    val repo = viewModel.addressRepository
    val addresses by repo.addresses.collectAsState(initial = emptyList())
    val selectedId by repo.selectedId.collectAsState(initial = null)
    val displayed = addresses.firstOrNull { it.id == selectedId }
        ?: addresses.firstOrNull { it.isDefault }
        ?: addresses.firstOrNull()

    // Orders — sourced from the singleton repo via the holder VM. MainShell
    // already prefetches on first composition, so Home just observes the
    // StateFlows.
    val orderRepo = viewModel.orderRepository
    val recentOrders by orderRepo.orders.collectAsState(initial = emptyList())
    val ordersLoaded by orderRepo.loaded.collectAsState(initial = false)
    val ordersLoading by orderRepo.loading.collectAsState(initial = false)

    // Loyalty — observe the account snapshot for the milestone card. MainShell's
    // prefetch is gated on the one-way `loaded` latch, so it fires at most once
    // per session; points earned after that reach this card only via the
    // staleness-gated ON_START refresh below (or a Rewards pull-to-refresh).
    // Null while loading or for guests.
    val loyaltyRepo = viewModel.loyaltyRepository
    val loyaltyAccount by loyaltyRepo.account.collectAsState(initial = null)
    // Credit comes from the same loyalty read the Rewards card and the Profile row show.
    val credit by loyaltyRepo.credit.collectAsState(initial = null)
    val referralAccount by viewModel.referralRepository.account.collectAsState(initial = null)

    // Push permission, re-read whenever Home resumes — returning from the system settings page with
    // notifications on removes the carousel's notifications slide.
    val context = androidx.compose.ui.platform.LocalContext.current
    var notificationsEnabled by remember {
        mutableStateOf(androidx.core.app.NotificationManagerCompat.from(context).areNotificationsEnabled())
    }
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) {
        notificationsEnabled = androidx.core.app.NotificationManagerCompat.from(context).areNotificationsEnabled()
    }
    val permissionScope = androidx.compose.runtime.rememberCoroutineScope()
    val notificationPermission = androidx.activity.compose.rememberLauncherForActivityResult(
        androidx.activity.result.contract.ActivityResultContracts.RequestPermission(),
    ) { granted ->
        notificationsEnabled = androidx.core.app.NotificationManagerCompat.from(context).areNotificationsEnabled()
        // Refused from the slide itself: the next tap opens the settings page, which can grant it
        // whether this refusal was for good or the dialog was only dismissed.
        if (!granted) permissionScope.launch { viewModel.appSettings.markNotificationPermissionRefused() }
    }
    // The system dialog only when it can still appear; a permission refused for good, or
    // notifications switched off in settings, can only be undone on the settings page.
    val onTurnOnNotifications: () -> Unit = {
        permissionScope.launch {
            val activity = context.findActivity()
            val permission = android.Manifest.permission.POST_NOTIFICATIONS
            val asks = activity != null && asksForNotificationPermission(
                sdkInt = android.os.Build.VERSION.SDK_INT,
                granted = androidx.core.content.ContextCompat.checkSelfPermission(context, permission) ==
                    android.content.pm.PackageManager.PERMISSION_GRANTED,
                showsRationale = androidx.core.app.ActivityCompat.shouldShowRequestPermissionRationale(activity, permission),
                refusedBefore = viewModel.appSettings.hasRefusedNotificationPermission(),
            )
            if (asks) notificationPermission.launch(permission) else openAppNotificationSettings(context)
        }
    }

    // Membership — drives the Plus upsell card visibility in the smart
    // carousel. This effect only covers the cold case; re-fetching an already
    // warm snapshot is the ON_START refresh's job. Null/false → show the
    // upsell; true → hide it.
    val membershipRepo = viewModel.membershipRepository
    val membership by membershipRepo.current.collectAsState(initial = null)
    androidx.compose.runtime.LaunchedEffect(Unit) {
        if (membership == null) membershipRepo.refresh()
    }
    val isPlus = membership?.hasMembership == true
    val plusTrialDays by viewModel.plusTrialDays.collectAsStateWithLifecycle()
    val plusDiscountPercent by viewModel.plusDiscountPercent.collectAsStateWithLifecycle()
    // The member's own free-cancellation window, the figure MembershipPerks lists, read by the confirm
    // step's rule: 0 hides the slide, and so do a renewal that failed, since no benefit runs then, and
    // a window no shorter than the standard one everyone gets, which is no benefit to advertise.
    val memberCancellationHours = cancellationPolicyFor(membership).plusFreeHours ?: 0

    // Catalog — used for the popular-packages quick-book strip. Home prices the chosen market
    // (ADR-0058 D5): refresh on first composition when nothing is loaded, whenever the market
    // changes, and whenever the repository still answers for another market — both wizards hand
    // the market back on exit, but Home must not depend on that.
    val catalogRepo = viewModel.catalogRepository
    val packages by catalogRepo.packages.collectAsState(initial = emptyList())
    val marketState by viewModel.marketRepository.state.collectAsStateWithLifecycle()
    val marketCountryId = marketState.countryId
    // Credit only pays an order in its own currency, so the slide offers the balance held in the
    // currency Home prices in: the market's, else the catalogue default the server prices.
    val catalogCurrency by catalogRepo.currencyCode.collectAsState()
    val homeCurrency = (marketState as? MarketState.Resolved)?.selected?.currencyCode ?: catalogCurrency
    val creditHere = credit?.balances?.firstOrNull {
        it.balance > 0.0 && homeCurrency != null && it.currencyCode.equals(homeCurrency, ignoreCase = true)
    }
    val referralCredit = marketState.selectedOrNull?.formattedReferralCredit()
    androidx.compose.runtime.LaunchedEffect(marketCountryId) {
        if (packages.isEmpty() || catalogRepo.countryId.value != marketCountryId) viewModel.refreshCatalog()
    }
    androidx.compose.runtime.LaunchedEffect(marketCountryId, isPlus) {
        if (!isPlus) viewModel.refreshPlusPlans()
    }
    // Top-3 packages by displayOrder (proxy for popularity) — falls back to
    // first 3 if displayOrder is null/uniform across the catalog.
    val popularPackages = androidx.compose.runtime.remember(packages) {
        packages
            .filter { !it.id.isNullOrBlank() }
            .take(3)
    }

    // Recurring schedules — observed so we can decide between the "active
    // schedules" section vs. the "set up recurring" carousel slide. Fetched for
    // everyone: a lapsed membership does not stop a schedule, and gating the
    // fetch on Plus hid a running schedule from the customer paying for it.
    val recurringRepo = viewModel.recurringBookingRepository
    val recurringTemplates by recurringRepo.templates.collectAsState(initial = emptyList())
    androidx.compose.runtime.LaunchedEffect(Unit) {
        recurringRepo.refresh()
    }
    // A schedule that needs a payment change books nothing until the customer fixes it, so it is
    // listed first rather than cut by the three-row limit.
    val activeRecurring = androidx.compose.runtime.remember(recurringTemplates) {
        recurringTemplates
            .filter { it.isActive }
            .sortedByDescending { ScheduleStatus.of(it) == ScheduleStatus.NeedsPaymentChange }
            .take(3)
    }
    val showRecurringSection = activeRecurring.isNotEmpty()
    val showSetupRecurringSlide = isPlus && membership?.benefitsPaused != true && recurringTemplates.isEmpty()
    // The express perk is the booking wizard's own verdict, so the slide and the wizard cannot disagree.
    val expressWaiver = resolveExpressWaiver(membership)
    val expressRemaining = expressWaiver.remaining.takeIf {
        expressWaiver.status == ExpressWaiverStatus.Available && membership?.benefitsPaused != true
    } ?: 0

    // Most recent Completed order — drives the "Order again" quick-action card.
    val mostRecentCompleted = androidx.compose.runtime.remember(recentOrders) {
        recentOrders.firstOrNull { orderStatusFromValue(it.orderStatus?.value) == OrderStatus.Completed }
    }

    // Sort locally by cleaningDateTime desc for defensiveness — the backend
    // list endpoint already returns recent-first, but null-safe local sorting
    // protects the UI from wire-order drift.
    val recentForDisplay = androidx.compose.runtime.remember(recentOrders) {
        recentOrders
            .sortedByDescending { it.cleaningDateTime ?: "" }
            .take(3)
    }

    // Render-gate: skip the section entirely if we have nothing to show. A
    // blank "Recent" block on day-1 signals emptiness the hero/presets don't.
    val showRecent = recentForDisplay.isNotEmpty() && (ordersLoaded || !ordersLoading)

    // First-paint gate — render a skeleton until the three critical sources
    // (orders, membership, catalog packages) have all returned. Without this
    // the home page renders piecemeal as each network call lands, causing
    // the visible layout to shift (carousel slides change, OrderAgain pops
    // in, recurring section appears) — bad UX per user feedback.
    //
    // Once `firstPaintReady` flips to true we never revert it for this tab
    // session, so refreshing data after the first paint just re-flows the
    // existing sections rather than dropping back into the skeleton.
    var firstPaintReady by androidx.compose.runtime.remember { androidx.compose.runtime.mutableStateOf(false) }
    val packagesReady = packages.isNotEmpty()
    val membershipReady = membership != null
    androidx.compose.runtime.LaunchedEffect(ordersLoaded, membershipReady, packagesReady) {
        if (ordersLoaded && membershipReady && packagesReady) {
            firstPaintReady = true
        }
    }
    // Hard ceiling — if any source is slow/failing, stop blocking after 1.5s
    // and render whatever we have. Better to show a partial real layout than
    // sit on a skeleton forever.
    androidx.compose.runtime.LaunchedEffect(Unit) {
        kotlinx.coroutines.delay(1500)
        firstPaintReady = true
    }

    // Notifications inbox behind the Home bell. Badge = server unread count,
    // refetched on Home entry + app foreground (both hit ON_START) and bumped
    // locally by the FCM receive path while the app runs.
    var showNotifications by androidx.compose.runtime.remember { androidx.compose.runtime.mutableStateOf(false) }
    val unreadNotifications by viewModel.notificationFeedRepository.unreadCount
        .collectAsStateWithLifecycle()
    // Home entry + foreground. The badge is cheap enough to refetch every time;
    // loyalty / orders / membership are not, so onResume gates each on its own
    // staleness watermark — this effect fires on every recomposition that
    // re-attaches the observer, not just once per foreground.
    LifecycleEventEffect(Lifecycle.Event.ON_START) {
        viewModel.refreshNotificationBadge()
        viewModel.onResume()
    }

    if (!firstPaintReady) {
        HomeSkeleton(modifier = modifier)
        return
    }

    val isRefreshing by viewModel.isUserRefreshing.collectAsStateWithLifecycle()
    val pullState = rememberPullToRefreshState()
    val scrollState = rememberScrollState()

    // Wrapped here rather than around the whole composable: the firstPaintReady branch above
    // returns early, and a gesture attached before it would be dead during first paint.
    PullToRefreshBox(
        isRefreshing = isRefreshing,
        onRefresh = viewModel::pullToRefresh,
        state = pullState,
        modifier = modifier.fillMaxSize(),
        indicator = {
            // The box runs under the status bar (Home is edge-to-edge), so the indicator rests
            // below the status-bar inset, where the other tabs' indicators rest below their title.
            SudsRefreshIndicator(
                state = pullState,
                isRefreshing = isRefreshing,
                modifier = Modifier
                    .align(Alignment.TopCenter)
                    .windowInsetsPadding(WindowInsets.statusBars)
                    .padding(top = 8.dp),
            )
        },
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .background(MaterialTheme.colorScheme.background)
                .statusBarFade(scrollState)
                .verticalScroll(scrollState)
                // Inside the scroll, so the address bar starts below the status bar at rest and
                // scrolls under it, where statusBarFade fades it out.
                .windowInsetsPadding(WindowInsets.statusBars),
        ) {
            // 1. Address bar + market chip + bell
            AddressTopBar(
                displayedAddress = displayed?.oneLine,
                market = (marketState as? MarketState.Resolved)?.takeIf { it.offersAChoice }?.selected,
                unreadCount = unreadNotifications,
                onAddressClick = onOpenAddressManager,
                onMarketClick = onOpenMarket,
                onNotificationClick = { showNotifications = true },
            )
            Spacer(Modifier.height(8.dp))

            if (showNotifications) {
                NotificationsInboxSheet(
                    onDismiss = { showNotifications = false },
                    onOpenRoute = { route ->
                        showNotifications = false
                        onOpenNotificationRoute(route)
                    },
                )
            }

            // 2. Smart upsell carousel. Slides show and hide by state; see upsellKinds.
            SmartUpsellCarousel(
                isPlus = isPlus,
                plusTrialDays = plusTrialDays,
                plusDiscountPercent = plusDiscountPercent,
                memberCancellationHours = memberCancellationHours,
                showSetupRecurring = showSetupRecurringSlide,
                notificationsOff = !notificationsEnabled,
                credit = creditHere,
                creditShare = credit?.maxShareOfOrder ?: 0.0,
                expressRemaining = expressRemaining,
                referralCode = referralAccount?.code?.takeIf { it.isNotBlank() },
                referralCredit = referralCredit,
                onTurnOnNotifications = onTurnOnNotifications,
                onSubscribePlus = onSubscribePlus,
                onBookCleaning = onBookCleaning,
                onOpenReferral = onOpenReferral,
                onShareReferral = { code ->
                    cz.cleansia.customer.features.rewards.shareReferralOrFallback(
                        context,
                        code,
                        viewModel::onReferralShareUnavailable,
                    )
                },
                onSetupRecurring = onSetupRecurring,
                onBookSize = onBookSize,
            )
            Spacer(Modifier.height(20.dp))

            // 3. Order again — replaces the static trust strip with a more useful
            // single-tap rebook of the most recent Completed order. Falls back to
            // the trust strip when the user has nothing to rebook (new accounts,
            // or accounts whose history is all in-progress).
            if (mostRecentCompleted != null) {
                OrderAgainCard(
                    order = mostRecentCompleted,
                    onClick = { mostRecentCompleted.id?.let(onRebookOrder) },
                )
            } else {
                TrustStrip()
            }
            Spacer(Modifier.height(24.dp))

            // 4. Recurring schedules, when at least one is unpaused.
            if (showRecurringSection) {
                RecurringSchedulesSection(
                    templates = activeRecurring,
                    onManage = onManageRecurring,
                )
                Spacer(Modifier.height(24.dp))
            }

            // 5. Popular packages — replaces the old static "Standard / Deep /
            // Move-out" presets. Tapping a card opens the booking sheet with the
            // package already selected (single tap → booking flow).
            if (popularPackages.isNotEmpty()) {
                PopularPackagesSection(
                    packages = popularPackages,
                    onPackageClick = onBookPackage,
                )
                Spacer(Modifier.height(24.dp))
            }

            // 5. Recent bookings — real orders from OrderRepository.
            if (showRecent) {
                RecentBookingsSection(
                    orders = recentForDisplay,
                    onOrderClick = onOrderClick,
                    onSeeAll = onSeeAllOrders,
                )
                Spacer(Modifier.height(24.dp))
            }

            // 6. Milestone progress — driven by loyalty lifetime points against the
            // tier ladder. Hide entirely when the account hasn't loaded yet (guest
            // or in-flight prefetch) or when the user already sits at the top tier
            // (nextTier == null) — no "next" to progress toward.
            loyaltyAccount?.let { account ->
                if (account.nextTier != null && account.pointsToNextTier != null) {
                    MilestoneProgressCard(account)
                    Spacer(Modifier.height(16.dp))
                }
            }

            // Clears the floating island bottom nav and its Book FAB.
            Spacer(Modifier.navigationBarsPadding().height(MainShellBottomClearance))
        }
    }
}

/* ── 1. Address top bar ── */

/**
 * [market] is non-null only when the directory offers a choice: a chip that opens a one-row picker
 * is a dead end, and with no market resolved nothing renders a guessed unit (ADR-0058 D3, D6).
 */
@Composable
private fun AddressTopBar(
    displayedAddress: String?,
    market: MarketListItem?,
    unreadCount: Int,
    onAddressClick: () -> Unit,
    onMarketClick: () -> Unit,
    onNotificationClick: () -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(start = 20.dp, end = 8.dp, top = 12.dp, bottom = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Row(
            modifier = Modifier
                .weight(1f)
                .clip(RoundedCornerShape(12.dp))
                .clickable(onClick = onAddressClick)
                .padding(vertical = 6.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(Icons.Outlined.LocationOn, null, tint = MaterialTheme.colorScheme.primary, modifier = Modifier.size(20.dp))
            Spacer(Modifier.width(6.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    stringResource(R.string.home_address_label),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        displayedAddress ?: stringResource(R.string.home_address_placeholder),
                        style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                        color = MaterialTheme.colorScheme.onBackground,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.weight(1f, fill = false),
                    )
                    Icon(Icons.Outlined.KeyboardArrowDown, null, tint = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.size(18.dp))
                }
            }
        }
        if (market != null) {
            Spacer(Modifier.width(8.dp))
            val marketA11y = stringResource(
                R.string.market_chip_a11y,
                localizedName(market.translations, market.name),
                market.currencyCode,
            )
            CleansiaChip(
                label = "${market.isoAlpha2} \u00B7 ${market.currencyCode}",
                isSelected = false,
                onClick = onMarketClick,
                role = Role.Button,
                modifier = Modifier.semantics { contentDescription = marketA11y },
            )
            Spacer(Modifier.width(8.dp))
        }
        IconButton(onClick = onNotificationClick) {
            Box(modifier = Modifier.size(40.dp)) {
                Box(
                    modifier = Modifier
                        .size(40.dp)
                        .background(MaterialTheme.colorScheme.surface, CircleShape)
                        .border(1.dp, MaterialTheme.colorScheme.outlineVariant, CircleShape),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(
                        Icons.Outlined.NotificationsNone,
                        contentDescription = if (unreadCount > 0) {
                            stringResource(R.string.notifications_bell_unread_content_description, unreadCount)
                        } else {
                            stringResource(R.string.notifications_inbox_title)
                        },
                        tint = MaterialTheme.colorScheme.onSurface,
                        modifier = Modifier.size(20.dp),
                    )
                }
                if (unreadCount > 0) {
                    Box(
                        modifier = Modifier
                            .align(Alignment.TopEnd)
                            .background(MaterialTheme.colorScheme.error, RoundedCornerShape(999.dp))
                            .padding(horizontal = 4.dp, vertical = 1.dp),
                        contentAlignment = Alignment.Center,
                    ) {
                        Text(
                            text = if (unreadCount > 99) {
                                stringResource(R.string.notifications_badge_overflow)
                            } else {
                                unreadCount.toString()
                            },
                            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                            color = MaterialTheme.colorScheme.onError,
                        )
                    }
                }
            }
        }
    }
}

/* ── 2. Smart upsell carousel — state-driven swipeable cards ──
 *
 * The home upsell shelf. State drives which slides appear and their order; see [upsellKinds].
 * -> /product/features
 */

internal enum class UpsellKind {
    Notifications,
    Credit,
    Express,
    SetupRecurring,
    Plus,
    Referral,
    PlusCancellation,
    ExpressToday,
    Rewards,
    ArrivalTimes,
    QuickSize,
}

/** How many slides come before the quick-size closer, which always shows. */
internal const val UPSELL_LEADING_CAP = 4

/**
 * Which slides show, in order — most relevant first, so the slide on screen at t=0 is the one the
 * customer is most likely to act on. The first [UPSELL_LEADING_CAP] eligible slides show, then the
 * quick-size slide closes the set: five every time, for every customer. The state-driven slides come
 * first; the "did you know" facts after referral fill the rest, and give way first. Referral, rewards
 * and arrival times are always eligible, and express-today is whenever the express slide is not, so
 * four leading slides always exist.
 */
internal fun upsellKinds(
    notificationsOff: Boolean,
    hasCredit: Boolean,
    expressAvailable: Boolean,
    showSetupRecurring: Boolean,
    isPlus: Boolean,
    /** The member's free-cancellation window in hours; 0 for a non-member or one whose benefits are paused. */
    memberCancellationHours: Int = 0,
): List<UpsellKind> = buildList {
    if (notificationsOff) add(UpsellKind.Notifications)
    if (hasCredit) add(UpsellKind.Credit)
    if (expressAvailable) add(UpsellKind.Express)
    if (showSetupRecurring) add(UpsellKind.SetupRecurring)
    if (!isPlus) add(UpsellKind.Plus)
    add(UpsellKind.Referral)
    if (isPlus && memberCancellationHours > 0) add(UpsellKind.PlusCancellation)
    // The express slide already says it for a member with a waiver left.
    if (!expressAvailable) add(UpsellKind.ExpressToday)
    add(UpsellKind.Rewards)
    add(UpsellKind.ArrivalTimes)
}.take(UPSELL_LEADING_CAP) + UpsellKind.QuickSize

/** Every slide its own drawing, so no two visible slides repeat a mascot. Same mapping as iOS. */
@androidx.annotation.DrawableRes
internal fun UpsellKind.mascotRes(): Int = when (this) {
    UpsellKind.Notifications -> R.drawable.mascot_waving
    UpsellKind.Credit -> R.drawable.mascot_invoice
    UpsellKind.Express -> R.drawable.mascot_floor_scrubber
    UpsellKind.SetupRecurring -> R.drawable.mascot_idea
    UpsellKind.Plus -> R.drawable.mascot_plus
    UpsellKind.Referral -> R.drawable.mascot_thumbs_up
    UpsellKind.PlusCancellation -> R.drawable.mascot_leaning
    UpsellKind.ExpressToday -> R.drawable.mascot_ready
    UpsellKind.Rewards -> R.drawable.mascot_spray_and_cloth
    UpsellKind.ArrivalTimes -> R.drawable.mascot_resting
    UpsellKind.QuickSize -> R.drawable.mascot_vacuuming
}

/**
 * One card: an eyebrow, a title, a two-line description, and a fact chip in the corner above the
 * mascot — an icon, with the slide's figure beside it when it has one.
 */
private data class UpsellSlide(
    val kind: UpsellKind,
    val top: String,
    val title: String,
    val description: String,
    val chipIcon: ImageVector,
    val chipText: String?,
    val cta: String,
    val gradient: List<Color>,
    val onClick: () -> Unit,
)

/** Every slide is this tall, so the pager never changes height between slides. The skeleton matches. */
private val UpsellCardHeight = 196.dp

/** The mascot's side; the fact chip sits in the same column above it. */
private val UpsellMascotSize = 84.dp

/** What the eyebrow, the description and the CTA leave free at the card's end: the mascot's column and a 4dp gap. */
private val UpsellMascotColumn = UpsellMascotSize + 4.dp

/** The first and the last arrival time the booking offers, for the arrival-times slide: 08:00 and 19:45. */
internal fun upsellArrivalBounds(): Pair<String, String> {
    val last = LAST_WINDOW_HOUR * 60 - BOOKING_SLOT_INTERVAL_MINUTES
    return "%02d:00".format(java.util.Locale.ROOT, FIRST_WINDOW_HOUR) to
        "%02d:%02d".format(java.util.Locale.ROOT, last / 60, last % 60)
}

@Composable
private fun SmartUpsellCarousel(
    isPlus: Boolean,
    plusTrialDays: Int,
    /** The headline plan's discount in whole percent; 0 while unknown, and the Plus slide then names none. */
    plusDiscountPercent: Int,
    /** The member's free-cancellation window in hours; 0 hides that slide. */
    memberCancellationHours: Int,
    showSetupRecurring: Boolean,
    notificationsOff: Boolean,
    /** The balance held in Home's currency, or null when there is none to spend here. */
    credit: CreditBalanceDto?,
    creditShare: Double,
    /** Express waivers left this month; 0 when the member has none (or is not one). */
    expressRemaining: Int,
    /** The customer's referral code once it has loaded; null falls back to opening Rewards. */
    referralCode: String?,
    /** The chosen market's referral credit, formatted; null when the market pays none or is not known yet. */
    referralCredit: String?,
    onTurnOnNotifications: () -> Unit,
    onSubscribePlus: () -> Unit,
    onBookCleaning: () -> Unit,
    onOpenReferral: () -> Unit,
    onShareReferral: (code: String) -> Unit,
    onSetupRecurring: () -> Unit,
    onBookSize: (rooms: Int, bathrooms: Int) -> Unit,
) {
    // Resolve gradient pairs at the composable level — BrandGradients.*() are
    // @Composable (they read LocalAppSettings for the theme override).
    val plusGradient = listOf(
        cz.cleansia.customer.ui.theme.Sky950,
        cz.cleansia.customer.ui.theme.Slate900,
    )
    val purpleGradient = cz.cleansia.customer.ui.theme.BrandGradients.purple().toList()
    val cyanGradient = cz.cleansia.customer.ui.theme.BrandGradients.cyan().toList()
    val blueGradient = cz.cleansia.customer.ui.theme.BrandGradients.blue().toList()
    val orangeGradient = cz.cleansia.customer.ui.theme.BrandGradients.orange().toList()
    val emeraldGradient = cz.cleansia.customer.ui.theme.BrandGradients.emerald().toList()

    val kinds = upsellKinds(
        notificationsOff = notificationsOff,
        hasCredit = credit != null,
        expressAvailable = expressRemaining > 0,
        showSetupRecurring = showSetupRecurring,
        isPlus = isPlus,
        memberCancellationHours = memberCancellationHours,
    )
    val sharePercent = (creditShare * 100).roundToInt()
    val offersTrial = plusTrialDays > 0
    val expressLead = BookingPricing.EXPRESS_LEAD_HOURS.toInt()
    val standardLead = BookingPricing.STANDARD_LEAD_HOURS.toInt()
    val didYouKnow = stringResource(R.string.home_upsell_did_you_know)
    val bookCta = stringResource(R.string.home_upsell_book_cta)
    val slides = kinds.map { kind ->
        when (kind) {
            UpsellKind.Notifications -> UpsellSlide(
                kind = kind,
                top = stringResource(R.string.home_upsell_notifications_top),
                title = stringResource(R.string.home_upsell_notifications_title),
                description = stringResource(R.string.home_upsell_notifications_desc),
                chipIcon = Icons.Outlined.NotificationsNone,
                chipText = null,
                cta = stringResource(R.string.home_upsell_notifications_cta),
                gradient = orangeGradient,
                onClick = onTurnOnNotifications,
            )
            // The server's balance and share, never a figure of the copy's own; it is spent
            // automatically, so the slide just opens booking.
            UpsellKind.Credit -> UpsellSlide(
                kind = kind,
                top = stringResource(R.string.credit_your_credit),
                title = stringResource(
                    R.string.home_upsell_credit_title,
                    formatOrderPrice(credit?.balance ?: 0.0, credit?.currencyCode),
                ),
                description = stringResource(R.string.home_upsell_credit_desc, sharePercent),
                chipIcon = Icons.Outlined.AccountBalanceWallet,
                chipText = null,
                cta = bookCta,
                gradient = emeraldGradient,
                onClick = onBookCleaning,
            )
            // Members only: Plus waives the express surcharge N times a month, on a slot 2–4 h out.
            UpsellKind.Express -> UpsellSlide(
                kind = kind,
                top = stringResource(R.string.home_upsell_express_top, expressLead, standardLead),
                title = pluralStringResource(R.plurals.home_upsell_express_title, expressRemaining, expressRemaining),
                description = stringResource(R.string.home_upsell_express_desc),
                chipIcon = Icons.Outlined.Bolt,
                chipText = stringResource(R.string.home_upsell_chip_times, expressRemaining),
                cta = bookCta,
                gradient = plusGradient,
                onClick = onBookCleaning,
            )
            // Setup-recurring — only for Plus subscribers who haven't yet built
            // a schedule. Surfaces the headline Plus perk so it doesn't get
            // stuck behind a tab.
            UpsellKind.SetupRecurring -> UpsellSlide(
                kind = kind,
                top = stringResource(R.string.home_upsell_setup_recurring_top),
                title = stringResource(R.string.home_upsell_setup_recurring_title),
                description = stringResource(R.string.home_upsell_setup_recurring_desc),
                chipIcon = Icons.Outlined.Repeat,
                chipText = null,
                cta = stringResource(R.string.home_upsell_setup_recurring_cta),
                gradient = purpleGradient,
                onClick = onSetupRecurring,
            )
            // The discount is the headline plan's, read from the server; until the plans load the
            // slide names the two benefits no plan configures.
            UpsellKind.Plus -> UpsellSlide(
                kind = kind,
                top = stringResource(R.string.home_upsell_plus_top),
                title = if (offersTrial) {
                    stringResource(R.string.home_upsell_plus_title_trial, plusTrialDays)
                } else {
                    stringResource(R.string.home_upsell_plus_title)
                },
                description = if (plusDiscountPercent > 0) {
                    stringResource(R.string.home_upsell_plus_desc, plusDiscountPercent)
                } else {
                    stringResource(R.string.home_upsell_plus_desc_generic)
                },
                chipIcon = Icons.Outlined.Star,
                chipText = plusDiscountPercent.takeIf { it > 0 }
                    ?.let { stringResource(R.string.home_upsell_chip_percent_off, it) },
                cta = stringResource(if (offersTrial) R.string.home_upsell_plus_cta_trial else R.string.home_upsell_plus_cta),
                // Same gradient as the Plus subscribe page hero — tapping
                // the card visually previews where the user lands.
                gradient = plusGradient,
                onClick = onSubscribePlus,
            )
            // The CTA says "Share my code", so the card shares it; until the code has loaded it
            // opens Rewards, where the code appears.
            UpsellKind.Referral -> UpsellSlide(
                kind = kind,
                top = stringResource(R.string.home_upsell_referral_top),
                title = stringResource(R.string.home_upsell_referral_title),
                description = referralCredit?.let { stringResource(R.string.home_upsell_referral_desc, it) }
                    ?: stringResource(R.string.home_upsell_referral_desc_generic),
                chipIcon = Icons.Outlined.CardGiftcard,
                chipText = referralCredit?.let { stringResource(R.string.home_upsell_chip_credit, it) },
                cta = stringResource(R.string.home_upsell_referral_cta),
                gradient = cyanGradient,
                onClick = { referralCode?.let(onShareReferral) ?: onOpenReferral() },
            )
            // "Did you know?" — the member's own window, as the membership reports it.
            UpsellKind.PlusCancellation -> UpsellSlide(
                kind = kind,
                top = didYouKnow,
                title = stringResource(R.string.home_upsell_plus_cancel_title, memberCancellationHours),
                description = stringResource(R.string.home_upsell_plus_cancel_desc),
                chipIcon = Icons.Outlined.EventAvailable,
                chipText = stringResource(R.string.home_upsell_chip_hours, memberCancellationHours),
                cta = bookCta,
                gradient = purpleGradient,
                onClick = onBookCleaning,
            )
            // The express band is the booking policy's (BookingPricing mirrors it, and a test pins it).
            UpsellKind.ExpressToday -> UpsellSlide(
                kind = kind,
                top = didYouKnow,
                title = stringResource(R.string.home_upsell_express_today_title, expressLead),
                description = stringResource(R.string.home_upsell_express_today_desc, standardLead),
                chipIcon = Icons.Outlined.Bolt,
                chipText = stringResource(R.string.home_upsell_chip_hours, expressLead),
                cta = bookCta,
                gradient = orangeGradient,
                onClick = onBookCleaning,
            )
            UpsellKind.Rewards -> UpsellSlide(
                kind = kind,
                top = didYouKnow,
                title = stringResource(R.string.home_upsell_rewards_title),
                description = stringResource(R.string.home_upsell_rewards_desc),
                chipIcon = Icons.Outlined.EmojiEvents,
                chipText = null,
                cta = stringResource(R.string.home_upsell_rewards_cta),
                gradient = emeraldGradient,
                onClick = onOpenReferral,
            )
            // The booking's own window and grid, the times the When step offers.
            UpsellKind.ArrivalTimes -> {
                val (first, last) = upsellArrivalBounds()
                UpsellSlide(
                    kind = kind,
                    top = didYouKnow,
                    title = stringResource(R.string.home_upsell_times_title),
                    description = stringResource(R.string.home_upsell_times_desc, first, last),
                    chipIcon = Icons.Outlined.Schedule,
                    chipText = stringResource(R.string.home_upsell_chip_minutes, BOOKING_SLOT_INTERVAL_MINUTES),
                    cta = bookCta,
                    gradient = cyanGradient,
                    onClick = onBookCleaning,
                )
            }
            // The closer replaces the old generic "Book" slide, which only duplicated the FAB.
            UpsellKind.QuickSize -> UpsellSlide(
                kind = kind,
                top = "",
                title = stringResource(R.string.home_quick_size_title),
                description = "",
                chipIcon = Icons.Outlined.Home,
                chipText = null,
                cta = stringResource(R.string.home_quick_size_cta),
                gradient = blueGradient,
                onClick = {},
            )
        }
    }

    // The carousel loops: a bounded run of virtual pages, each showing slides[page mod n], opened in
    // the middle on slide 0, so a swipe past the last slide lands on the first and there is no edge to
    // rubber-band on. Bounded rather than Int.MAX_VALUE so TalkBack's scroll range stays sane. iOS
    // gets the same loop from sentinel clones, because TabView's page style is not lazy.
    val slideCount = slides.size
    val pagerState = rememberPagerState(initialPage = upsellAnchor(slideCount)) { upsellVirtualCount(slideCount) }
    val pagerScope = androidx.compose.runtime.rememberCoroutineScope()

    // A slide arriving or leaving after first paint (membership, credit, the notification setting)
    // changes the set; re-anchor so the slide on screen stays the one the customer was looking at.
    var anchoredKinds by remember { mutableStateOf(kinds) }
    androidx.compose.runtime.LaunchedEffect(kinds) {
        if (anchoredKinds != kinds) {
            val target = upsellReanchor(pagerState.currentPage, anchoredKinds, kinds)
            anchoredKinds = kinds
            pagerState.scrollToPage(target)
        }
    }

    // Auto-advance every 6s, always forward. It stops for the rest of this Home visit once the
    // customer swipes or taps (WCAG 2.2.2: moving content they can stop), and never runs under
    // TalkBack or with animations removed. One long-lived loop keyed on n alone: every input it
    // reads is a snapshot read inside it, so a state arrival does not cancel the pending delay.
    var userInteracted by remember { mutableStateOf(false) }
    val dragged by pagerState.interactionSource.collectIsDraggedAsState()
    androidx.compose.runtime.LaunchedEffect(dragged) {
        if (dragged) userInteracted = true
    }
    val context = androidx.compose.ui.platform.LocalContext.current
    val autoRotateMs = 6_000L
    androidx.compose.runtime.LaunchedEffect(slideCount) {
        if (slideCount <= 1) return@LaunchedEffect
        while (true) {
            kotlinx.coroutines.delay(autoRotateMs)
            if (userInteracted) return@LaunchedEffect
            if (!pagerState.isScrollInProgress && upsellAutoAdvanceAllowed(context)) {
                pagerState.animateScrollToPage(pagerState.currentPage + 1)
            }
        }
    }
    val goToPage: (Int) -> Unit = { page ->
        userInteracted = true
        pagerScope.launch { pagerState.animateScrollToPage(page) }
    }

    // The quick-size slide's own steppers. Starting where the booking wizard starts, and bounded by
    // the same caps, so "See my price" opens a booking the server accepts.
    var quickRooms by androidx.compose.runtime.saveable.rememberSaveable { mutableStateOf(1) }
    var quickBathrooms by androidx.compose.runtime.saveable.rememberSaveable { mutableStateOf(1) }

    Column {
        // No contentPadding here — earlier version used 20dp peek-ahead so
        // the previous/next slides showed at the edges, which read as a
        // rendering bug rather than a discoverability hint. Slides now snap
        // to full viewport width; the per-slide horizontal padding lives
        // inside UpsellSlideCard so the card itself still has 20dp gutter.
        HorizontalPager(
            state = pagerState,
            pageSpacing = 0.dp,
        ) { page ->
            val logical = upsellLogical(page, slideCount)
            val slide = slides[logical]
            val position = stringResource(R.string.home_upsell_page_a11y, logical + 1, slideCount)
            val nextLabel = stringResource(R.string.home_upsell_next)
            val previousLabel = stringResource(R.string.home_upsell_previous)
            val pageSemantics = Modifier.semantics {
                stateDescription = position
                if (slideCount > 1) {
                    customActions = listOf(
                        CustomAccessibilityAction(nextLabel) { goToPage(page + 1); true },
                        CustomAccessibilityAction(previousLabel) { goToPage(page - 1); true },
                    )
                }
            }
            if (slide.kind == UpsellKind.QuickSize) {
                QuickSizeSlideCard(
                    slide = slide,
                    rooms = quickRooms,
                    bathrooms = quickBathrooms,
                    onRoomsChange = {
                        userInteracted = true
                        quickRooms = it.coerceIn(1, PropertySize.MAX_ROOMS)
                    },
                    onBathroomsChange = {
                        userInteracted = true
                        quickBathrooms = it.coerceIn(1, PropertySize.MAX_BATHROOMS)
                    },
                    onSeePrice = {
                        userInteracted = true
                        onBookSize(quickRooms, quickBathrooms)
                    },
                    modifier = pageSemantics,
                )
            } else {
                UpsellSlideCard(
                    slide = slide.copy(
                        onClick = {
                            userInteracted = true
                            slide.onClick()
                        },
                    ),
                    modifier = pageSemantics,
                )
            }
        }
        // Dot indicator — active segment grows wider, no fill animation.
        // Hidden when there's only one slide (no swipe affordance needed). Silent to TalkBack: the
        // card states its own position.
        if (slides.size > 1) {
            Spacer(Modifier.height(10.dp))
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clearAndSetSemantics {},
                horizontalArrangement = Arrangement.Center,
            ) {
                repeat(slides.size) { idx ->
                    val selected = upsellLogical(pagerState.currentPage, slideCount) == idx
                    val width by animateDpAsState(
                        targetValue = if (selected) 24.dp else 8.dp,
                        label = "upsell-dot-$idx",
                    )
                    Box(
                        modifier = Modifier
                            .padding(horizontal = 3.dp)
                            .size(width = width, height = 8.dp)
                            .clip(RoundedCornerShape(999.dp))
                            .background(
                                if (selected) MaterialTheme.colorScheme.primary
                                else MaterialTheme.colorScheme.outlineVariant,
                            ),
                    )
                }
            }
        }
    }
}

/**
 * The loop's page arithmetic, top-level so it is testable. n slides become n × 1000 virtual pages
 * (none when there is nothing to loop), opened on a page in the middle that shows slide 0.
 */
internal fun upsellVirtualCount(n: Int): Int = if (n > 1) n * 1000 else n

internal fun upsellAnchor(n: Int): Int {
    if (n <= 1) return 0
    val middle = upsellVirtualCount(n) / 2
    return middle - middle.mod(n)
}

internal fun upsellLogical(page: Int, n: Int): Int = if (n <= 0) 0 else page.mod(n)

/**
 * The page to jump to when the slide set changes: the slide on screen if it is still in the set —
 * slides arrive at the front, so its index moves — else the same position, or the last slide if the
 * set shrank past it.
 */
internal fun upsellReanchor(page: Int, old: List<UpsellKind>, new: List<UpsellKind>): Int {
    if (new.isEmpty()) return 0
    val logical = upsellLogical(page, old.size)
    val index = old.getOrNull(logical)?.let { new.indexOf(it) }?.takeIf { it >= 0 }
        ?: minOf(logical, new.size - 1)
    return upsellAnchor(new.size) + index
}

/** Auto-advance is moving content: never under TalkBack, never with animations removed. */
private fun upsellAutoAdvanceAllowed(context: android.content.Context): Boolean {
    val a11y = context.getSystemService(android.view.accessibility.AccessibilityManager::class.java)
    if (a11y?.isTouchExplorationEnabled == true) return false
    val animatorScale = android.provider.Settings.Global.getFloat(
        context.contentResolver,
        android.provider.Settings.Global.ANIMATOR_DURATION_SCALE,
        1f,
    )
    return animatorScale > 0f
}

/**
 * The card inside 196dp: 20dp padding leaves 156dp. The column stacks the eyebrow (18) + 6 + the title
 * (two 24sp lines, 48) + 4 + the description (two 16sp lines, 32) + the CTA pill (36), 144dp, with the
 * slack above the pill. The mascot's [UpsellMascotSize] square starts 72dp down, where the title's
 * second line ends, so the title runs the full width; the fact chip sits above the mascot, and the
 * eyebrow, the description and the pill stop short of that column. A title or description too long for
 * its two lines steps down to 80% before it ellipsises, as iOS's minimumScaleFactor(0.8) does.
 */
@Composable
private fun UpsellSlideCard(slide: UpsellSlide, modifier: Modifier = Modifier) {
    // Outer padding lives on the slide (not the pager) so each page snaps
    // full-width with no peek-ahead from neighbors. 20dp matches the
    // horizontal gutter used elsewhere on the home tab.
    Box(
        modifier = modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp)
            .height(UpsellCardHeight)
            .clip(RoundedCornerShape(22.dp))
            .background(Brush.linearGradient(slide.gradient))
            .clickable(onClick = slide.onClick)
            .padding(20.dp),
    ) {
        Image(
            painter = painterResource(slide.kind.mascotRes()),
            contentDescription = null,
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .size(UpsellMascotSize),
        )
        UpsellFactChip(
            icon = slide.chipIcon,
            text = slide.chipText,
            modifier = Modifier.align(Alignment.TopEnd),
        )
        Column(modifier = Modifier.fillMaxHeight()) {
            Text(
                slide.top,
                style = MaterialTheme.typography.labelMedium,
                color = Color.White.copy(alpha = 0.85f),
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
                modifier = Modifier.padding(end = UpsellMascotColumn),
            )
            Spacer(Modifier.height(6.dp))
            UpsellFittedText(
                slide.title,
                style = MaterialTheme.typography.headlineSmall.copy(fontFamily = Poppins, fontWeight = FontWeight.Bold),
                color = Color.White,
            )
            Spacer(Modifier.height(4.dp))
            UpsellFittedText(
                slide.description,
                style = MaterialTheme.typography.bodySmall,
                color = Color.White.copy(alpha = 0.9f),
                modifier = Modifier.padding(end = UpsellMascotColumn),
            )
            Spacer(Modifier.weight(1f))
            UpsellCtaPill(slide.cta, modifier = Modifier.padding(end = UpsellMascotColumn))
        }
    }
}

/** Two lines at most; a text that does not fit them shrinks by 10% twice before it ellipsises. */
@Composable
private fun UpsellFittedText(
    text: String,
    style: androidx.compose.ui.text.TextStyle,
    color: Color,
    modifier: Modifier = Modifier,
) {
    var shrinkSteps by remember(text) { mutableIntStateOf(0) }
    val scale = 1f - 0.1f * shrinkSteps
    Text(
        text,
        style = style.copy(fontSize = style.fontSize * scale, lineHeight = style.lineHeight * scale),
        color = color,
        maxLines = 2,
        overflow = TextOverflow.Ellipsis,
        onTextLayout = { if (it.hasVisualOverflow && shrinkSteps < 2) shrinkSteps++ },
        modifier = modifier,
    )
}

/** The slide's fact: an icon, and its figure when it has one ("−5%", "2 h", "+150"). */
@Composable
private fun UpsellFactChip(icon: ImageVector, text: String?, modifier: Modifier = Modifier) {
    Row(
        modifier = modifier
            .background(Color.White.copy(alpha = 0.22f), RoundedCornerShape(999.dp))
            // 24dp tall (an 18sp line + 3dp either side), so it ends where the title's first line begins.
            .padding(horizontal = if (text == null) 6.dp else 10.dp, vertical = 3.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(icon, null, tint = Color.White, modifier = Modifier.size(16.dp))
        if (text != null) {
            Spacer(Modifier.width(4.dp))
            Text(
                text,
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.ExtraBold),
                color = Color.White,
                maxLines = 1,
            )
        }
    }
}

@Composable
private fun UpsellCtaPill(text: String, modifier: Modifier = Modifier) {
    Row(
        modifier = modifier
            .background(Color.White.copy(alpha = 0.22f), RoundedCornerShape(999.dp))
            .padding(horizontal = 14.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text,
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = Color.White,
        )
        Spacer(Modifier.width(6.dp))
        Icon(Icons.AutoMirrored.Outlined.ArrowForward, null, tint = Color.White, modifier = Modifier.size(14.dp))
    }
}

/**
 * "How big is your home?" — two −/+ capsules and a "See my price" button that opens booking with the
 * size already set. Taps only (a drag would fight the pager), and the card itself is not a button, so
 * a stepper tap never opens booking. The mascot sits bottom-right beside the price button, under the
 * steppers rather than behind them, so the steppers keep the card's full width (as on iOS). Top-right
 * it shared its 72 dp band with the capsules and drew under the bathrooms "+".
 */
@Composable
private fun QuickSizeSlideCard(
    slide: UpsellSlide,
    rooms: Int,
    bathrooms: Int,
    onRoomsChange: (Int) -> Unit,
    onBathroomsChange: (Int) -> Unit,
    onSeePrice: () -> Unit,
    modifier: Modifier = Modifier,
) {
    var titleLines by remember { mutableIntStateOf(1) }
    Box(
        modifier = modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp)
            .height(UpsellCardHeight)
            .clip(RoundedCornerShape(22.dp))
            .background(Brush.linearGradient(slide.gradient))
            .padding(20.dp),
    ) {
        Image(
            painter = painterResource(slide.kind.mascotRes()),
            contentDescription = null,
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .size(quickSizeMascotSize(titleLines)),
        )
        Column(modifier = Modifier.fillMaxWidth()) {
            // Full width now that nothing sits beside it: a title that wraps pushes the steppers down,
            // and the mascot shrinks to stay below them (quickSizeMascotSize).
            Text(
                slide.title,
                style = MaterialTheme.typography.headlineSmall.copy(fontFamily = Poppins, fontWeight = FontWeight.Bold),
                color = Color.White,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
                onTextLayout = { titleLines = it.lineCount },
                modifier = Modifier.fillMaxWidth(),
            )
            // The steppers and the price button are 48 dp touch rows around 36 dp capsules: this
            // spacer is 6 dp short of the 8 dp gap it draws, and the 12 dp between the steppers and
            // the button is their two margins, so the card still fits UpsellCardHeight.
            Spacer(Modifier.height(2.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                QuickSizeStepper(
                    label = pluralStringResource(R.plurals.booking_rooms_short, rooms, rooms),
                    lessLabel = stringResource(R.string.home_quick_size_rooms_less),
                    moreLabel = stringResource(R.string.home_quick_size_rooms_more),
                    canRemove = rooms > 1,
                    canAdd = rooms < PropertySize.MAX_ROOMS,
                    onMinus = { onRoomsChange(rooms - 1) },
                    onPlus = { onRoomsChange(rooms + 1) },
                    modifier = Modifier.weight(1f),
                )
                QuickSizeStepper(
                    label = pluralStringResource(R.plurals.booking_bath_short, bathrooms, bathrooms),
                    lessLabel = stringResource(R.string.home_quick_size_baths_less),
                    moreLabel = stringResource(R.string.home_quick_size_baths_more),
                    canRemove = bathrooms > 1,
                    canAdd = bathrooms < PropertySize.MAX_BATHROOMS,
                    onMinus = { onBathroomsChange(bathrooms - 1) },
                    onPlus = { onBathroomsChange(bathrooms + 1) },
                    modifier = Modifier.weight(1f),
                )
            }
            UpsellCtaPill(
                slide.cta,
                modifier = Modifier
                    .minimumInteractiveComponentSize()
                    .clip(RoundedCornerShape(999.dp))
                    .clickable(role = Role.Button, onClick = onSeePrice),
            )
        }
    }
}

/**
 * The quick-size mascot's side, which keeps it clear of the steppers above it. The title is 18sp on
 * 24sp lines and at most two of them; under it come a 2dp spacer and the 48dp stepper row, whose 36dp
 * capsules sit 6dp in. On one line the capsules end 68dp down the card's 156dp inner height, above a
 * 72dp mascot's top at 84dp. A wrapped title (uk and ru on a 360dp phone, more locales on a narrower
 * one) ends them at 92dp, 8dp into a 72dp mascot, so the mascot is 56dp then and starts at 100dp.
 */
internal fun quickSizeMascotSize(titleLines: Int): Dp = if (titleLines > 1) 56.dp else 72.dp

@Composable
private fun QuickSizeStepper(
    label: String,
    lessLabel: String,
    moreLabel: String,
    canRemove: Boolean,
    canAdd: Boolean,
    onMinus: () -> Unit,
    onPlus: () -> Unit,
    modifier: Modifier = Modifier,
) {
    // A 36 dp capsule, but each −/+ answers a 48 dp square centred on its icon: the 6 dp above and
    // below are this row's padding, and the 4 dp either side reach into the label's margin and the
    // gap beside the capsule, so the label keeps its width. Nothing here clips — a clip would stop
    // the overhang taking taps — and the ripple is an 18 dp circle that stays inside the capsule.
    Row(
        modifier = modifier
            .padding(vertical = 6.dp)
            .height(36.dp)
            .background(Color.White.copy(alpha = 0.22f), RoundedCornerShape(999.dp)),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier
                .size(40.dp, 36.dp)
                .requiredSize(48.dp)
                .clickable(
                    interactionSource = null,
                    indication = ripple(bounded = false, radius = 18.dp),
                    enabled = canRemove,
                    role = Role.Button,
                    onClick = onMinus,
                )
                .semantics { contentDescription = lessLabel },
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.Remove,
                null,
                tint = Color.White.copy(alpha = if (canRemove) 1f else 0.38f),
                modifier = Modifier.size(16.dp),
            )
        }
        // On a 360dp phone the label has 56dp, and uk and ru counts do not fit one line ("1 ванна
        // кімната", "2 комнаты"), so it takes two 16sp lines, which the 36dp capsule holds. A word still
        // too wide for its line steps the size down to 80%, as iOS's minimumScaleFactor(0.8) does.
        var shrinkSteps by remember(label) { mutableIntStateOf(0) }
        val labelStyle = MaterialTheme.typography.labelMedium
        Text(
            label,
            style = labelStyle.copy(
                fontWeight = FontWeight.SemiBold,
                fontSize = labelStyle.fontSize * (1f - 0.1f * shrinkSteps),
                lineHeight = 16.sp,
            ),
            color = Color.White,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis,
            textAlign = TextAlign.Center,
            onTextLayout = { if (it.hasVisualOverflow && shrinkSteps < 2) shrinkSteps++ },
            modifier = Modifier.weight(1f),
        )
        Box(
            Modifier
                .size(40.dp, 36.dp)
                .requiredSize(48.dp)
                .clickable(
                    interactionSource = null,
                    indication = ripple(bounded = false, radius = 18.dp),
                    enabled = canAdd,
                    role = Role.Button,
                    onClick = onPlus,
                )
                .semantics { contentDescription = moreLabel },
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.Add,
                null,
                tint = Color.White.copy(alpha = if (canAdd) 1f else 0.38f),
                modifier = Modifier.size(16.dp),
            )
        }
    }
}

/**
 * Whether the notifications slide raises the system dialog rather than the settings page: only on
 * API 33+ with the permission not granted, and only while the dialog can still appear. Android shows
 * a rationale between the first and the second refusal; a permission never refused shows none but can
 * still be asked, and one refused for good shows none and cannot, which [refusedBefore] tells apart.
 * Notifications switched off in settings with the permission granted are the settings page's too.
 */
internal fun asksForNotificationPermission(
    sdkInt: Int,
    granted: Boolean,
    showsRationale: Boolean,
    refusedBefore: Boolean,
): Boolean = sdkInt >= android.os.Build.VERSION_CODES.TIRAMISU && !granted && (showsRationale || !refusedBefore)

/** The system's notification page for this app — where a permanently refused permission is undone. */
private fun openAppNotificationSettings(context: android.content.Context) {
    val intent = android.content.Intent(android.provider.Settings.ACTION_APP_NOTIFICATION_SETTINGS)
        .putExtra(android.provider.Settings.EXTRA_APP_PACKAGE, context.packageName)
    if (context.findActivity() == null) intent.addFlags(android.content.Intent.FLAG_ACTIVITY_NEW_TASK)
    try {
        context.startActivity(intent)
    } catch (_: android.content.ActivityNotFoundException) {
        val details = android.content.Intent(
            android.provider.Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
            android.net.Uri.fromParts("package", context.packageName, null),
        ).addFlags(android.content.Intent.FLAG_ACTIVITY_NEW_TASK)
        runCatching { context.startActivity(details) }
    }
}

private tailrec fun android.content.Context.findActivity(): android.app.Activity? = when (this) {
    is android.app.Activity -> this
    is android.content.ContextWrapper -> baseContext.findActivity()
    else -> null
}

/* ── 3. Trust strip ── */

@Composable
private fun TrustStrip() {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp)
            .clip(RoundedCornerShape(14.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
            .padding(horizontal = 12.dp, vertical = 12.dp),
        horizontalArrangement = Arrangement.spacedBy(0.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        TrustItem(Icons.Outlined.Bolt, stringResource(R.string.home_trust_same_day), modifier = Modifier.weight(1f))
    }
}

@Composable
private fun TrustItem(
    icon: ImageVector,
    label: String,
    modifier: Modifier = Modifier,
    tint: Color = SuccessText,
) {
    Column(
        modifier = modifier,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Icon(icon, null, tint = tint, modifier = Modifier.size(20.dp))
        Spacer(Modifier.height(4.dp))
        Text(
            label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurface,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
            textAlign = TextAlign.Center,
        )
    }
}

/* ── 4. New home sections — Order Again, Recurring Schedules, Popular Packages ── */

/**
 * "Order again" — single-card quick rebook of the user's most recent
 * Completed order. Replaces the static trust strip when the user has at
 * least one completed order to repeat. Tap opens the booking sheet
 * pre-filled with the same services + address.
 */
@Composable
private fun OrderAgainCard(order: OrderListItemDto, onClick: () -> Unit) {
    val title = recentBookingTitle(
        order = order,
        fallback = stringResource(R.string.home_order_again_fallback_title),
    )
    val whenText = order.cleaningDateTime?.let { iso ->
        runCatching {
            val instant = java.time.Instant.parse(iso)
            java.time.format.DateTimeFormatter
                .ofPattern("MMM d", java.util.Locale.getDefault())
                .withZone(java.time.ZoneId.systemDefault())
                .format(instant)
        }.getOrNull()
    }

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp)
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(16.dp))
            .clickable(onClick = onClick)
            .padding(14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(44.dp)
                .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(Icons.Outlined.Refresh, null, tint = MaterialTheme.colorScheme.primary, modifier = Modifier.size(22.dp))
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                stringResource(R.string.home_order_again_title),
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = primaryText(),
            )
            Spacer(Modifier.height(2.dp))
            Text(
                title,
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            if (!whenText.isNullOrBlank()) {
                Text(
                    stringResource(R.string.home_order_again_subtitle, whenText),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
        }
        Icon(
            Icons.AutoMirrored.Outlined.ArrowForward,
            null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(20.dp),
        )
    }
}

/**
 * Unpaused recurring schedules — mini list with a "Manage" link. A cash schedule the
 * server skips until it is changed carries its badge here too, since a customer who
 * never opens the recurring list is told nowhere else.
 */
@Composable
private fun RecurringSchedulesSection(
    templates: List<cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto>,
    onManage: () -> Unit,
) {
    Column {
        Row(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            SectionTitle(stringResource(R.string.home_recurring_section_title), Modifier.weight(1f))
            Text(
                stringResource(R.string.home_recurring_section_manage),
                style = MaterialTheme.typography.labelLarge,
                color = primaryText(),
                modifier = Modifier.clickable(onClick = onManage),
            )
        }
        Spacer(Modifier.height(10.dp))
        Column(
            modifier = Modifier.padding(horizontal = 20.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            templates.forEach { template ->
                RecurringScheduleRow(template = template, onClick = onManage)
            }
        }
    }
}

@Composable
private fun RecurringScheduleRow(
    template: cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto,
    onClick: () -> Unit,
) {
    val freq = cz.cleansia.customer.core.recurring.RecurrenceFrequency.fromCode(template.frequency)
    val cadenceLabel = stringResource(
        when (freq) {
            cz.cleansia.customer.core.recurring.RecurrenceFrequency.Weekly -> R.string.recurring_bookings_cadence_weekly
            cz.cleansia.customer.core.recurring.RecurrenceFrequency.Biweekly -> R.string.recurring_bookings_cadence_biweekly
            cz.cleansia.customer.core.recurring.RecurrenceFrequency.Monthly -> R.string.recurring_bookings_cadence_monthly
        },
    )
    val javaDow = if (template.dayOfWeek == 0) 7 else template.dayOfWeek
    val dayName = java.time.DayOfWeek.of(javaDow)
        .getDisplayName(java.time.format.TextStyle.FULL, java.util.Locale.getDefault())
    val schedule = stringResource(R.string.recurring_bookings_day_at_time, dayName, template.timeOfDay)
    val needsChange = ScheduleStatus.of(template) == ScheduleStatus.NeedsPaymentChange
    val accent = if (needsChange) MaterialTheme.colorScheme.onSurfaceVariant else MaterialTheme.colorScheme.primary

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
            .clickable(onClick = onClick)
            .padding(12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(40.dp)
                .background(accent.copy(alpha = 0.12f), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.AutoAwesome,
                null,
                tint = accent,
                modifier = Modifier.size(20.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                cadenceLabel,
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = if (needsChange) accent else primaryText(),
            )
            Text(
                schedule,
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            if (!template.addressLine.isNullOrBlank()) {
                Text(
                    template.addressLine,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
            }
        }
        if (needsChange) {
            Spacer(Modifier.width(8.dp))
            Text(
                stringResource(R.string.recurring_status_needs_change),
                style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier
                    .clip(RoundedCornerShape(8.dp))
                    .background(MaterialTheme.colorScheme.surfaceVariant)
                    .padding(horizontal = 8.dp, vertical = 3.dp),
            )
            Spacer(Modifier.width(8.dp))
        }
        Icon(
            Icons.AutoMirrored.Outlined.ArrowForward,
            null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(18.dp),
        )
    }
}

/**
 * Popular packages — top-3 from the catalog. Tap adds the package to the
 * booking flow and opens the wizard at step 2 (sheet seeds
 * selectedPackageIds). Replaces the old static "Standard / Deep / Moveout"
 * presets that all routed to a blank booking sheet.
 */
@Composable
private fun PopularPackagesSection(
    packages: List<cz.cleansia.customer.core.catalog.PackageListItem>,
    onPackageClick: (String) -> Unit,
) {
    Column {
        SectionTitle(stringResource(R.string.home_popular_packages_title), Modifier.padding(horizontal = 20.dp))
        Spacer(Modifier.height(10.dp))
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp)
                .height(androidx.compose.foundation.layout.IntrinsicSize.Max),
            horizontalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            packages.forEach { pkg ->
                PopularPackageCard(
                    pkg = pkg,
                    modifier = Modifier.weight(1f).fillMaxHeight(),
                    onClick = { pkg.id?.let(onPackageClick) },
                )
            }
        }
    }
}

@Composable
private fun PopularPackageCard(
    pkg: cz.cleansia.customer.core.catalog.PackageListItem,
    modifier: Modifier,
    onClick: () -> Unit,
) {
    Column(
        modifier = modifier
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(18.dp))
            .clickable(onClick = onClick)
            .padding(14.dp),
    ) {
        Box(
            modifier = Modifier
                .size(40.dp)
                .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.CleaningServices,
                null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }
        Spacer(Modifier.height(10.dp))
        Text(
            text = localizedName(pkg.translations, pkg.name.orEmpty()),
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis,
        )
        Spacer(Modifier.height(4.dp))
        Text(
            text = stringResource(R.string.home_popular_packages_add_cta),
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = primaryText(),
        )
    }
}

/* ── 5. Recent bookings — tap-to-view most recent orders ── */

@Composable
private fun RecentBookingsSection(
    orders: List<OrderListItemDto>,
    onOrderClick: (String) -> Unit,
    onSeeAll: () -> Unit,
) {
    Column {
        Row(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 20.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            SectionTitle(stringResource(R.string.home_recent_title), Modifier.weight(1f))
            Text(
                stringResource(R.string.home_recent_see_all),
                style = MaterialTheme.typography.labelLarge,
                color = primaryText(),
                modifier = Modifier.clickable(onClick = onSeeAll),
            )
        }
        Spacer(Modifier.height(10.dp))
        Column(
            modifier = Modifier.padding(horizontal = 20.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            orders.forEach { order ->
                RecentBookingRow(
                    order = order,
                    onClick = { order.id?.let(onOrderClick) },
                )
            }
        }
    }
}

/**
 * Extract a human-readable title for the row: first service name, falling back
 * to the first package name, with "+ N more" suffix if the order has multiple
 * items. Names resolve to the active locale's translation when the order
 * snapshot carries one (T-0395). Defensive against the fully-nullable wire shape.
 */
@Composable
private fun recentBookingTitle(order: OrderListItemDto, fallback: String): String {
    val names = (
        order.selectedServices.orEmpty().mapNotNull { svc ->
            svc.name?.takeIf { it.isNotBlank() }?.let { localizedName(svc.translations, it) }
        } +
            order.selectedPackages.orEmpty().mapNotNull { pkg ->
                pkg.name?.takeIf { it.isNotBlank() }?.let { localizedName(pkg.translations, it) }
            }
        )
    if (names.isEmpty()) return fallback
    val first = names.first()
    val remaining = names.size - 1
    return if (remaining > 0) {
        "$first ${pluralStringResource(R.plurals.orders_services_more, remaining, remaining)}"
    } else {
        first
    }
}

@Composable
private fun RecentBookingRow(
    order: OrderListItemDto,
    onClick: () -> Unit,
) {
    val statusColor = orderStatusColor(order.orderStatus?.value)
    val title = recentBookingTitle(order, fallback = stringResource(R.string.home_recent_fallback_title))
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
            .clickable(onClick = onClick)
            .padding(14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(40.dp)
                .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.CleaningServices,
                null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    title,
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    modifier = Modifier.weight(1f, fill = false),
                )
                val statusLabelRes = orderStatusLabelRes(order.orderStatus?.value)
                val statusLabel = statusLabelRes?.let { stringResource(it) }
                    ?: order.orderStatus?.name?.takeIf { it.isNotBlank() }
                statusLabel?.let { label ->
                    Spacer(Modifier.width(8.dp))
                    Row(
                        modifier = Modifier
                            .clip(RoundedCornerShape(999.dp))
                            .background(statusColor.copy(alpha = 0.14f))
                            .padding(horizontal = 8.dp, vertical = 2.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Text(
                            label,
                            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
                            color = statusColor,
                        )
                    }
                }
            }
            Text(
                "${formatOrderDateTime(order.cleaningDateTime)} · ${formatOrderPrice(order.totalPrice, order.currency?.code)}",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Spacer(Modifier.width(8.dp))
        Icon(
            Icons.AutoMirrored.Outlined.ArrowForwardIos,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(14.dp),
        )
    }
}

/* ── 6. Milestone progress ── */

/**
 * Map a backend [LoyaltyTier] to its localized display label resource.
 * Mirror of the same mapping used by the Rewards tab.
 */
@Composable
private fun loyaltyTierLabel(tier: LoyaltyTier): String = stringResource(
    when (tier) {
        LoyaltyTier.BronzeCleaner -> R.string.loyalty_tier_bronze_cleaner
        LoyaltyTier.SilverMopper -> R.string.loyalty_tier_silver_mopper
        LoyaltyTier.GoldPolisher -> R.string.loyalty_tier_gold_polisher
        LoyaltyTier.PlatinumSparkler -> R.string.loyalty_tier_platinum_sparkler
    },
)

@Composable
private fun MilestoneProgressCard(account: LoyaltyAccountDto) {
    // Defensive null-handling: parent gates on `nextTier != null` and
    // `pointsToNextTier != null`, but we double-check here so the composable
    // is safe to call directly. Bail silently when either is missing.
    val nextTierEnum = LoyaltyTier.fromValue(account.nextTier) ?: return
    val pointsToNext = account.pointsToNextTier ?: return
    val currentTierEnum = LoyaltyTier.fromValue(account.currentTier) ?: LoyaltyTier.BronzeCleaner

    val lifetimePoints = account.lifetimePoints
    val targetPoints = lifetimePoints + pointsToNext
    val progress = if (targetPoints <= 0) 0f
        else (lifetimePoints.toFloat() / targetPoints.toFloat()).coerceIn(0f, 1f)

    val currentTierLabel = loyaltyTierLabel(currentTierEnum)
    val nextTierLabel = loyaltyTierLabel(nextTierEnum)

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp)
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.tertiaryContainer.copy(alpha = 0.4f))
            .padding(16.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(
                Icons.Outlined.Star,
                null,
                tint = WarningStar,
                modifier = Modifier.size(20.dp),
            )
            Spacer(Modifier.width(8.dp))
            Text(
                stringResource(R.string.home_milestone_title_v2, currentTierLabel),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onBackground,
                modifier = Modifier.weight(1f),
            )
            Text(
                "$lifetimePoints/$targetPoints",
                style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onBackground,
            )
        }
        Spacer(Modifier.height(8.dp))
        LinearProgressIndicator(
            progress = { progress },
            modifier = Modifier
                .fillMaxWidth()
                .height(6.dp)
                .clip(RoundedCornerShape(3.dp)),
            color = WarningStar,
            trackColor = MaterialTheme.colorScheme.outlineVariant,
        )
        Spacer(Modifier.height(6.dp))
        Text(
            pluralStringResource(
                R.plurals.home_milestone_subtitle_v2,
                pointsToNext,
                pointsToNext,
                nextTierLabel,
            ),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

/* ── Shared ── */

@Composable
private fun SectionTitle(text: String, modifier: Modifier = Modifier) {
    Text(
        text,
        style = MaterialTheme.typography.titleMedium.copy(fontFamily = Poppins, fontWeight = FontWeight.SemiBold),
        color = MaterialTheme.colorScheme.onBackground,
        modifier = modifier,
    )
}

/**
 * Skeleton placeholder shown while the first batch of critical data
 * (orders, membership, catalog) is in flight. Mimics the shape of the
 * real layout so the eventual swap doesn't push other content around.
 *
 * Uses a subtle pulsing alpha so the user reads it as "loading" rather
 * than "empty state". Hard 1.5s ceiling in the caller means even a
 * stalled network won't sit on this forever.
 */
@Composable
private fun HomeSkeleton(modifier: Modifier = Modifier) {
    val infiniteTransition = androidx.compose.animation.core.rememberInfiniteTransition(
        label = "skeleton-pulse",
    )
    val alpha by infiniteTransition.animateFloat(
        initialValue = 0.3f,
        targetValue = 0.6f,
        animationSpec = androidx.compose.animation.core.infiniteRepeatable(
            animation = androidx.compose.animation.core.tween(900),
            repeatMode = androidx.compose.animation.core.RepeatMode.Reverse,
        ),
        label = "skeleton-alpha",
    )
    val blockColor = MaterialTheme.colorScheme.outlineVariant.copy(alpha = alpha)

    Column(
        modifier = modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        // Address bar placeholder — matches the real AddressTopBar height
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(start = 20.dp, end = 16.dp, top = 16.dp, bottom = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            SkeletonBlock(
                modifier = Modifier
                    .weight(1f)
                    .height(40.dp),
                color = blockColor,
            )
            Spacer(Modifier.width(12.dp))
            SkeletonBlock(
                modifier = Modifier.size(40.dp),
                color = blockColor,
                shape = CircleShape,
            )
        }

        // Carousel slide placeholder — matches the real upsell card's height
        SkeletonBlock(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp)
                .height(UpsellCardHeight),
            color = blockColor,
            shape = RoundedCornerShape(22.dp),
        )
        Spacer(Modifier.height(28.dp))

        // Order Again / Trust Strip placeholder
        SkeletonBlock(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp)
                .height(72.dp),
            color = blockColor,
            shape = RoundedCornerShape(16.dp),
        )
        Spacer(Modifier.height(28.dp))

        // Section title placeholder + 3 cards row
        SkeletonBlock(
            modifier = Modifier
                .padding(horizontal = 20.dp)
                .height(20.dp)
                .width(160.dp),
            color = blockColor,
        )
        Spacer(Modifier.height(12.dp))
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp),
            horizontalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            repeat(3) {
                SkeletonBlock(
                    modifier = Modifier
                        .weight(1f)
                        .height(110.dp),
                    color = blockColor,
                    shape = RoundedCornerShape(18.dp),
                )
            }
        }
    }
}

@Composable
private fun SkeletonBlock(
    modifier: Modifier,
    color: androidx.compose.ui.graphics.Color,
    shape: androidx.compose.ui.graphics.Shape = RoundedCornerShape(8.dp),
) {
    Box(
        modifier = modifier
            .clip(shape)
            .background(color),
    )
}

@Preview(widthDp = 390, heightDp = 1400)
@Composable
private fun HomeTabPreview() {
    CleansiaTheme { HomeTab() }
}
