package cz.cleansia.customer.features.home

import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.animateFloat
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
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
import androidx.compose.foundation.layout.padding
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
import androidx.compose.material.icons.automirrored.outlined.ArrowForward
import androidx.compose.material.icons.automirrored.outlined.ArrowForwardIos
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material.icons.outlined.Bolt
import androidx.compose.material.icons.outlined.CalendarToday
import androidx.compose.material.icons.outlined.CleaningServices
import androidx.compose.material.icons.outlined.KeyboardArrowDown
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material.icons.outlined.NotificationsNone
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Refresh
import androidx.compose.material.icons.outlined.Shield
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material.icons.outlined.VerifiedUser
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
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
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.customer.ui.format.orderStatusColor
import cz.cleansia.customer.core.loyalty.LoyaltyAccountDto
import cz.cleansia.customer.core.loyalty.LoyaltyTier
import cz.cleansia.customer.core.orders.OrderListItemDto
import cz.cleansia.customer.features.booking.localizedName
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.core.ui.theme.Poppins
import cz.cleansia.customer.features.orders.OrderStatus
import cz.cleansia.customer.features.orders.orderStatusFromValue
import cz.cleansia.customer.features.orders.orderStatusLabelRes
import cz.cleansia.customer.ui.theme.SuccessText
import cz.cleansia.customer.ui.theme.WarningStar

/* ── Presentation models ── */

private data class PastCleaner(val id: String, val name: String, val rating: Float, val jobs: Int)

@Composable
fun HomeTab(
    modifier: Modifier = Modifier,
    onBookCleaning: () -> Unit = {},
    onViewAllServices: () -> Unit = {},
    onOpenAddressManager: () -> Unit = {},
    onOrderClick: (String) -> Unit = {},
    onSeeAllOrders: () -> Unit = {},
    onSubscribePlus: () -> Unit = {},
    onOpenReferral: () -> Unit = {},
    /** Tap on a popular-package card. Opens booking sheet pre-filled with the package. */
    onBookPackage: (String) -> Unit = {},
    /** Tap on the "Order again" quick-action card. Opens booking sheet pre-filled from the order. */
    onRebookOrder: (String) -> Unit = {},
    /** Tap on the "Set up recurring" affordance. Routes to the create wizard. */
    onSetupRecurring: () -> Unit = {},
    /** Tap on a recurring-schedule row. Routes to the management screen. */
    onManageRecurring: () -> Unit = {},
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

    // Loyalty — observe the account snapshot for the milestone card. MainShell
    // already prefetches LoyaltyRepository.refresh() on first composition, so
    // this is a pure observer; null while loading or for guests.
    val loyaltyRepo = viewModel.loyaltyRepository
    val loyaltyAccount by loyaltyRepo.account.collectAsState(initial = null)

    // Membership — drives the Plus upsell card visibility in the smart
    // carousel. Refresh once on first composition (no other surface refreshes
    // it on tab switch). Null/false → show the upsell; true → hide it.
    val membershipRepo = viewModel.membershipRepository
    val membership by membershipRepo.current.collectAsState(initial = null)
    androidx.compose.runtime.LaunchedEffect(Unit) {
        if (membership == null) membershipRepo.refresh()
    }
    val isPlus = membership?.hasMembership == true
    val hasAnyOrders = recentOrders.isNotEmpty()

    // Catalog — used for the popular-packages quick-book strip. Refresh once
    // on first composition; CatalogRepository.refresh is a no-op if cached.
    val catalogRepo = viewModel.catalogRepository
    val packages by catalogRepo.packages.collectAsState(initial = emptyList())
    androidx.compose.runtime.LaunchedEffect(Unit) {
        if (packages.isEmpty()) viewModel.refreshCatalog()
    }
    // Top-3 packages by displayOrder (proxy for popularity) — falls back to
    // first 3 if displayOrder is null/uniform across the catalog.
    val popularPackages = androidx.compose.runtime.remember(packages) {
        packages
            .filter { !it.id.isNullOrBlank() }
            .take(3)
    }

    // Recurring schedules — Plus perk; observed only so we can decide between
    // the "active schedules" section vs. the "set up recurring" carousel slide.
    // Refresh once per home composition for Plus users.
    val recurringRepo = viewModel.recurringBookingRepository
    val recurringTemplates by recurringRepo.templates.collectAsState(initial = emptyList())
    androidx.compose.runtime.LaunchedEffect(isPlus) {
        if (isPlus) recurringRepo.refresh()
    }
    val activeRecurring = androidx.compose.runtime.remember(recurringTemplates) {
        recurringTemplates.filter { it.isActive }.take(3)
    }
    val showRecurringSection = isPlus && activeRecurring.isNotEmpty()
    val showSetupRecurringSlide = isPlus && recurringTemplates.isEmpty()

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

    if (!firstPaintReady) {
        HomeSkeleton(modifier = modifier)
        return
    }

    Column(
        modifier = modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .windowInsetsPadding(WindowInsets.statusBars)
            .verticalScroll(rememberScrollState()),
    ) {
        // 1. Address bar + bell
        AddressTopBar(
            displayedAddress = displayed?.oneLine,
            onAddressClick = onOpenAddressManager,
            onNotificationClick = {},
        )
        Spacer(Modifier.height(8.dp))

        // 2. Smart upsell carousel — Plus / first-booking / referral / book /
        // setup-recurring. Slides hide based on user state.
        SmartUpsellCarousel(
            isPlus = isPlus,
            hasAnyOrders = hasAnyOrders,
            showSetupRecurring = showSetupRecurringSlide,
            onSubscribePlus = onSubscribePlus,
            onBookCleaning = onBookCleaning,
            onOpenReferral = onOpenReferral,
            onSetupRecurring = onSetupRecurring,
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

        // 4. Recurring schedules (Plus-only, when at least one is active) —
        // mini list with a "Manage" link. Lets users see what's already
        // booked-on-repeat without leaving home.
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

        // 7. Seasonal suggestion
        SeasonalCard(onBook = onBookCleaning)
        // Trailing inset reserves room for the floating island bottom nav so
        // the last card isn't hidden behind it. ~96dp pill height + 12dp gap.
        Spacer(Modifier.height(108.dp))
    }
}

/* ── 1. Address top bar ── */

@Composable
private fun AddressTopBar(
    displayedAddress: String?,
    onAddressClick: () -> Unit,
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
        IconButton(onClick = onNotificationClick) {
            Box(
                modifier = Modifier
                    .size(40.dp)
                    .background(MaterialTheme.colorScheme.surface, CircleShape)
                    .border(1.dp, MaterialTheme.colorScheme.outlineVariant, CircleShape),
                contentAlignment = Alignment.Center,
            ) {
                Icon(Icons.Outlined.NotificationsNone, null, tint = MaterialTheme.colorScheme.onSurface, modifier = Modifier.size(20.dp))
            }
        }
    }
}

/* ── 2. Smart upsell carousel — state-driven swipeable cards ──
 *
 * What it does: shows the most relevant upsell card first on home, in a
 * Wolt+/Bolt-style horizontally-swipeable shelf. State drives both which
 * slides appear and the order:
 *
 *  - Plus      → hidden when user is already a Plus subscriber. Pitches the
 *                14-day free trial. Routes to /membership/subscribe.
 *  - Welcome   → hidden once user has any past order (no point pitching the
 *                first-order promo to a return customer). Routes to booking.
 *  - Referral  → always visible. Routes to the Rewards tab where the
 *                referral block lives.
 *  - Book      → always visible as the steady-state "book a cleaning" CTA.
 *
 * Auto-rotates every 6s; pauses while the user is touching the pager.
 *
 * Design intent: the Plus card uses the same dark Sky950→Slate900 gradient
 * as the Plus subscribe page, so tapping it visually previews the destination.
 */

private enum class UpsellKind { Plus, Welcome, Referral, Book, SetupRecurring }

private data class UpsellSlide(
    val kind: UpsellKind,
    val topRes: Int,
    val titleRes: Int,
    val ctaRes: Int,
    val gradient: List<Color>,
    val mascotRes: Int,
    val onClick: () -> Unit,
)

@Composable
private fun SmartUpsellCarousel(
    isPlus: Boolean,
    hasAnyOrders: Boolean,
    showSetupRecurring: Boolean,
    onSubscribePlus: () -> Unit,
    onBookCleaning: () -> Unit,
    onOpenReferral: () -> Unit,
    onSetupRecurring: () -> Unit,
) {
    // Resolve gradient pairs at the composable level — BrandGradients.*() are
    // @Composable (they read LocalAppSettings for the theme override) and
    // can't be called from inside `remember`'s plain lambda.
    val plusGradient = listOf(
        cz.cleansia.customer.ui.theme.Sky950,
        cz.cleansia.customer.ui.theme.Slate900,
    )
    val (purpleA, purpleB) = cz.cleansia.customer.ui.theme.BrandGradients.purple()
    val purpleGradient = listOf(purpleA, purpleB)
    val (cyanA, cyanB) = cz.cleansia.customer.ui.theme.BrandGradients.cyan()
    val cyanGradient = listOf(cyanA, cyanB)
    val (blueA, blueB) = cz.cleansia.customer.ui.theme.BrandGradients.blue()
    val blueGradient = listOf(blueA, blueB)

    // Build the slide list from current state. Order matters: most-relevant
    // first so the slide on screen at t=0 is the one the user is most likely
    // to act on.
    val slides = androidx.compose.runtime.remember(
        isPlus, hasAnyOrders, showSetupRecurring,
        plusGradient, purpleGradient, cyanGradient, blueGradient,
    ) {
        buildList {
            if (!isPlus) {
                add(
                    UpsellSlide(
                        kind = UpsellKind.Plus,
                        topRes = R.string.home_upsell_plus_top,
                        titleRes = R.string.home_upsell_plus_title,
                        ctaRes = R.string.home_upsell_plus_cta,
                        // Same gradient as the Plus subscribe page hero — tapping
                        // the card visually previews where the user lands.
                        gradient = plusGradient,
                        mascotRes = R.drawable.mascot_ready,
                        onClick = onSubscribePlus,
                    ),
                )
            }
            // Setup-recurring — only for Plus subscribers who haven't yet built
            // a schedule. Surfaces the headline Plus perk so it doesn't get
            // stuck behind a tab.
            if (showSetupRecurring) {
                add(
                    UpsellSlide(
                        kind = UpsellKind.SetupRecurring,
                        topRes = R.string.home_upsell_setup_recurring_top,
                        titleRes = R.string.home_upsell_setup_recurring_title,
                        ctaRes = R.string.home_upsell_setup_recurring_cta,
                        gradient = purpleGradient,
                        mascotRes = R.drawable.mascot_idea,
                        onClick = onSetupRecurring,
                    ),
                )
            }
            if (!hasAnyOrders) {
                add(
                    UpsellSlide(
                        kind = UpsellKind.Welcome,
                        topRes = R.string.home_upsell_welcome_top,
                        titleRes = R.string.home_upsell_welcome_title,
                        ctaRes = R.string.home_upsell_welcome_cta,
                        gradient = purpleGradient,
                        mascotRes = R.drawable.mascot_mopping,
                        onClick = onBookCleaning,
                    ),
                )
            }
            add(
                UpsellSlide(
                    kind = UpsellKind.Referral,
                    topRes = R.string.home_upsell_referral_top,
                    titleRes = R.string.home_upsell_referral_title,
                    ctaRes = R.string.home_upsell_referral_cta,
                    gradient = cyanGradient,
                    mascotRes = R.drawable.mascot_cleaning,
                    onClick = onOpenReferral,
                ),
            )
            add(
                UpsellSlide(
                    kind = UpsellKind.Book,
                    topRes = R.string.home_hero_greeting,
                    titleRes = R.string.home_hero_prompt,
                    ctaRes = R.string.home_hero_cta,
                    gradient = blueGradient,
                    mascotRes = R.drawable.mascot_cleaning,
                    onClick = onBookCleaning,
                ),
            )
        }
    }

    val pagerState = rememberPagerState(pageCount = { slides.size })

    // Auto-rotate every 6s. The previous version keyed the LaunchedEffect on
    // `slides.size`, `currentPage`, AND `isScrollInProgress` — which meant
    // every async state arrival (membership refresh, order prefetch, the
    // animateScrollToPage call itself flipping isScrollInProgress) cancelled
    // the in-flight delay and the timer never got to fire.
    //
    // Fix: a single long-lived loop keyed only on `slides.size`. The loop
    // reads `currentPage` + `isScrollInProgress` from inside (snapshot reads)
    // so they don't act as cancellation triggers. Pause is implemented by
    // skipping the advance when the user is mid-drag — the next iteration
    // waits another 6s and tries again.
    val slideCount = slides.size
    val autoRotateMs = 6_000L
    androidx.compose.runtime.LaunchedEffect(slideCount) {
        if (slideCount <= 1) return@LaunchedEffect
        while (true) {
            kotlinx.coroutines.delay(autoRotateMs)
            if (!pagerState.isScrollInProgress) {
                val next = (pagerState.currentPage + 1) % slideCount
                pagerState.animateScrollToPage(next)
            }
            // If isScrollInProgress was true, we just skip this advance and
            // wait another 6s — gives the user time to settle their gesture.
        }
    }

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
            UpsellSlideCard(slide = slides[page])
        }
        // Dot indicator — active segment grows wider, no fill animation.
        // Hidden when there's only one slide (no swipe affordance needed).
        if (slides.size > 1) {
            Spacer(Modifier.height(10.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.Center,
            ) {
                repeat(slides.size) { idx ->
                    val selected = pagerState.currentPage == idx
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

@Composable
private fun UpsellSlideCard(slide: UpsellSlide) {
    // Outer padding lives on the slide (not the pager) so each page snaps
    // full-width with no peek-ahead from neighbors. 20dp matches the
    // horizontal gutter used elsewhere on the home tab.
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp)
            .height(180.dp)
            .clip(RoundedCornerShape(22.dp))
            .background(Brush.linearGradient(slide.gradient))
            .clickable(onClick = slide.onClick)
            .padding(20.dp),
    ) {
        Column(modifier = Modifier.fillMaxWidth(0.72f)) {
            Text(
                stringResource(slide.topRes),
                style = MaterialTheme.typography.labelLarge,
                color = Color.White.copy(alpha = 0.85f),
            )
            Spacer(Modifier.height(4.dp))
            Text(
                stringResource(slide.titleRes),
                style = MaterialTheme.typography.headlineSmall.copy(fontFamily = Poppins, fontWeight = FontWeight.Bold),
                color = Color.White,
            )
            Spacer(Modifier.height(14.dp))
            Row(
                modifier = Modifier
                    .background(Color.White.copy(alpha = 0.22f), RoundedCornerShape(999.dp))
                    .padding(horizontal = 14.dp, vertical = 8.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    stringResource(slide.ctaRes),
                    style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                    color = Color.White,
                )
                Spacer(Modifier.width(6.dp))
                Icon(Icons.AutoMirrored.Outlined.ArrowForward, null, tint = Color.White, modifier = Modifier.size(14.dp))
            }
        }
        Image(
            painter = painterResource(slide.mascotRes),
            contentDescription = null,
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .size(110.dp),
        )
    }
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
        TrustItem(Icons.Outlined.Shield, stringResource(R.string.home_trust_insured), modifier = Modifier.weight(1f))
        Box(Modifier.width(1.dp).height(28.dp).background(MaterialTheme.colorScheme.outlineVariant))
        TrustItem(Icons.Outlined.VerifiedUser, stringResource(R.string.home_trust_vetted), modifier = Modifier.weight(1f))
        Box(Modifier.width(1.dp).height(28.dp).background(MaterialTheme.colorScheme.outlineVariant))
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
                color = MaterialTheme.colorScheme.primary,
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
 * Active recurring schedules — Plus-only mini list with a "Manage" link.
 * Surfaces what's already booked-on-repeat so users don't have to dig
 * through the Profile tab to remember they have a schedule going.
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
                color = MaterialTheme.colorScheme.primary,
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
                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.AutoAwesome,
                null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                cadenceLabel,
                style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
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
            color = MaterialTheme.colorScheme.primary,
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
                color = MaterialTheme.colorScheme.primary,
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
 * items. Defensive against the fully-nullable wire shape.
 */
private fun recentBookingTitle(order: OrderListItemDto, fallback: String): String {
    val names = (order.selectedServices.orEmpty().mapNotNull { it.name?.takeIf { n -> n.isNotBlank() } } +
        order.selectedPackages.orEmpty().mapNotNull { it.name?.takeIf { n -> n.isNotBlank() } })
    if (names.isEmpty()) return fallback
    val first = names.first()
    val remaining = names.size - 1
    return if (remaining > 0) "$first + $remaining more" else first
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
            stringResource(R.string.home_milestone_subtitle_v2, pointsToNext, nextTierLabel),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

/* ── 7. Seasonal tip ── */

@Composable
private fun SeasonalCard(onBook: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp)
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.secondaryContainer.copy(alpha = 0.5f))
            .clickable(onClick = onBook)
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(44.dp)
                .background(MaterialTheme.colorScheme.secondary.copy(alpha = 0.15f), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.CalendarToday,
                null,
                tint = MaterialTheme.colorScheme.secondary,
                modifier = Modifier.size(22.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                stringResource(R.string.home_seasonal_title),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onBackground,
            )
            Text(
                stringResource(R.string.home_seasonal_subtitle),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Icon(
            Icons.AutoMirrored.Outlined.ArrowForward,
            null,
            tint = MaterialTheme.colorScheme.secondary,
            modifier = Modifier.size(18.dp),
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

        // Carousel slide placeholder — matches the real upsell card's 180dp height
        SkeletonBlock(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp)
                .height(180.dp),
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
