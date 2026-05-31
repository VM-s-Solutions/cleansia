package cz.cleansia.customer.features.rewards

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CardGiftcard
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.CloudOff
import androidx.compose.material.icons.outlined.ContentCopy
import androidx.compose.material.icons.outlined.Diamond
import androidx.compose.material.icons.outlined.EmojiEvents
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material.icons.outlined.MilitaryTech
import androidx.compose.material.icons.outlined.Share
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material.icons.outlined.Workspaces
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.material3.pulltorefresh.rememberPullToRefreshState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.customer.core.loyalty.LoyaltyAccountDto
import cz.cleansia.customer.core.loyalty.LoyaltyActivityItemDto
import cz.cleansia.customer.core.loyalty.LoyaltyEarnSource
import cz.cleansia.customer.core.loyalty.LoyaltyRepositoryEntryPoint
import cz.cleansia.customer.core.loyalty.LoyaltyTier
import cz.cleansia.customer.core.loyalty.LoyaltyTransactionType
import cz.cleansia.customer.core.loyalty.TierInfoDto
import cz.cleansia.customer.core.loyalty.TierPerkDto
import cz.cleansia.customer.core.referral.ReferralAccountDto
import cz.cleansia.customer.core.referral.ReferralRepositoryEntryPoint
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.snackbar.SnackbarControllerEntryPoint
import cz.cleansia.core.ui.components.SudsRefreshIndicator
import cz.cleansia.core.ui.theme.Poppins
import cz.cleansia.customer.ui.theme.SuccessText
import dagger.hilt.android.EntryPointAccessors
import kotlinx.coroutines.launch

/**
 * Rewards tab — Loyalty Phase A (M2).
 *
 * Read-only view of the signed-in user's loyalty state. Consumes the singleton
 * [cz.cleansia.customer.core.loyalty.LoyaltyRepository] which MainShell prefetches
 * on first composition (so this tab opens instantly when reached via the bottom
 * nav). The repo also caches the tier ladder; activity is paged on-demand here
 * (preview only — full pagination lives in [RewardsActivityScreen]).
 *
 * Sections, top to bottom:
 *  1. Hero — tier-themed gradient card with badge, name, lifetime points,
 *     completed bookings count.
 *  2. Progress — "X / Y points to {NextTier}" or max-tier celebration.
 *  3. Current perks — list of perks from the backend, label keys resolved via
 *     a runtime resolver (see [resolveLabelKey]).
 *  4. Tier ladder — all 4 tiers with threshold + discount summary + status pill.
 *  5. Recent activity — up to 5 most recent transactions + "See all" link.
 *
 * Phase A is read-only: no redemption, no referrals.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RewardsTab(
    modifier: Modifier = Modifier,
    onOpenActivity: () -> Unit = {},
) {
    val context = LocalContext.current
    // TODO(W3.3): refactor to VM injection — a small @HiltViewModel exposing
    // LoyaltyRepository + ReferralRepository would let this drop the
    // EntryPointAccessors detour.
    val loyaltyRepo = remember {
        EntryPointAccessors
            .fromApplication(context, LoyaltyRepositoryEntryPoint::class.java)
            .loyaltyRepository()
    }
    val referralRepo = remember {
        EntryPointAccessors
            .fromApplication(context, ReferralRepositoryEntryPoint::class.java)
            .referralRepository()
    }
    val scope = rememberCoroutineScope()

    val account by loyaltyRepo.account.collectAsState()
    val tiers by loyaltyRepo.tiers.collectAsState()
    val loading by loyaltyRepo.loading.collectAsState()
    val loaded by loyaltyRepo.loaded.collectAsState()
    val referralAccount by referralRepo.account.collectAsState()

    // Activity preview — up to 5 items shown inline. Fetched independently of
    // the cached account/tiers since [LoyaltyRepository] doesn't hold activity
    // (the activity sub-screen pages the whole list itself). Local state only.
    var activityPreview by remember { mutableStateOf<List<LoyaltyActivityItemDto>>(emptyList()) }
    LaunchedEffect(loaded) {
        if (loaded) {
            val resp = loyaltyRepo.loadActivity(offset = 0, limit = 5)
            activityPreview = resp?.data ?: emptyList()
        }
    }

    val pullState = rememberPullToRefreshState()
    val refresh: () -> Unit = {
        scope.launch {
            loyaltyRepo.refresh()
            // Pull-to-refresh also re-fetches the referral snapshot so the stats
            // counters stay current after a friend qualifies.
            referralRepo.refresh()
            // Re-prime the preview after a manual refresh — the repo doesn't cache it.
            val resp = loyaltyRepo.loadActivity(offset = 0, limit = 5)
            activityPreview = resp?.data ?: emptyList()
        }
    }

    Column(
        modifier = modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .windowInsetsPadding(WindowInsets.statusBars),
    ) {
        Text(
            stringResource(R.string.rewards_title),
            style = MaterialTheme.typography.headlineMedium.copy(
                fontFamily = Poppins,
                fontWeight = FontWeight.Bold,
            ),
            color = MaterialTheme.colorScheme.onBackground,
            modifier = Modifier.padding(horizontal = 20.dp, vertical = 16.dp),
        )

        PullToRefreshBox(
            isRefreshing = loading,
            onRefresh = refresh,
            state = pullState,
            modifier = Modifier.fillMaxSize(),
            indicator = {
                SudsRefreshIndicator(
                    state = pullState,
                    isRefreshing = loading,
                    modifier = Modifier
                        .align(Alignment.TopCenter)
                        .padding(top = 8.dp),
                )
            },
        ) {
            // First-load spinner — only the initial open. Once `loaded` flips
            // we fall through to the cached content so subsequent refreshes
            // show the pull-to-refresh indicator instead of replacing the UI.
            val loadedAccount = account
            when {
                loading && !loaded -> LoyaltyLoading()
                // Account fetch failed (or hasn't completed) and we have nothing
                // cached. Wrap in a scrollable so the pull-to-refresh gesture
                // attaches; same trick as OrdersTab.ScrollableStateContainer.
                loadedAccount == null -> ScrollableStateContainer { LoyaltyError(onRetry = refresh) }
                else -> LoyaltyContent(
                    account = loadedAccount,
                    tiers = tiers,
                    referralAccount = referralAccount,
                    activityPreview = activityPreview,
                    onOpenActivity = onOpenActivity,
                )
            }
        }
    }
}

/* ── Loaded content ── */

@Composable
private fun LoyaltyContent(
    account: LoyaltyAccountDto,
    tiers: List<TierInfoDto>,
    referralAccount: ReferralAccountDto?,
    activityPreview: List<LoyaltyActivityItemDto>,
    onOpenActivity: () -> Unit,
) {
    val currentTier = LoyaltyTier.fromValue(account.currentTier) ?: LoyaltyTier.BronzeCleaner

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp),
    ) {
        HeroCard(
            tier = currentTier,
            lifetimePoints = account.lifetimePoints,
            bookingsCount = account.completedBookingsCount,
        )
        Spacer(Modifier.height(16.dp))

        ProgressCard(account = account, currentTier = currentTier)
        Spacer(Modifier.height(16.dp))

        CurrentPerksCard(perks = account.currentPerks)
        Spacer(Modifier.height(16.dp))

        TierLadderCard(tiers = tiers, currentTier = currentTier)
        Spacer(Modifier.height(16.dp))

        // ── Loyalty Phase C — Invite friends card ──
        // Hidden when the code hasn't lazy-issued yet (first refresh in flight,
        // or the rare backend failure). MainShell prefetch + pull-to-refresh
        // both call referralRepo.refresh() which triggers EnsureCodeForUserAsync.
        if (referralAccount != null && referralAccount.code.isNotBlank()) {
            InviteFriendsCard(referralAccount)
            Spacer(Modifier.height(16.dp))
        }

        ActivityPreviewCard(
            activity = activityPreview,
            onOpenActivity = onOpenActivity,
        )
        // Reserve room for the floating island bottom nav.
        Spacer(Modifier.height(108.dp))
    }
}

/* ── Hero ── */

/** Per-tier gradient stop colors. Warm-brown → primary for Bronze, etc. */
private fun tierGradientColors(tier: LoyaltyTier): Pair<Color, Color> = when (tier) {
    LoyaltyTier.BronzeCleaner -> Color(0xFF92400E) to Color(0xFFC2671A) // brown → tan
    LoyaltyTier.SilverMopper -> Color(0xFF475569) to Color(0xFF94A3B8) // slate → silver
    LoyaltyTier.GoldPolisher -> Color(0xFFB45309) to Color(0xFFF59E0B) // amber-700 → amber-500
    LoyaltyTier.PlatinumSparkler -> Color(0xFF6D28D9) to Color(0xFFA78BFA) // purple → violet
}

/** Per-tier badge icon. All from material-icons-extended outlined set. */
private fun tierIcon(tier: LoyaltyTier): ImageVector = when (tier) {
    LoyaltyTier.BronzeCleaner -> Icons.Outlined.Workspaces
    LoyaltyTier.SilverMopper -> Icons.Outlined.MilitaryTech
    LoyaltyTier.GoldPolisher -> Icons.Outlined.EmojiEvents
    LoyaltyTier.PlatinumSparkler -> Icons.Outlined.Diamond
}

private fun tierLabelRes(tier: LoyaltyTier): Int = when (tier) {
    LoyaltyTier.BronzeCleaner -> R.string.loyalty_tier_bronze_cleaner
    LoyaltyTier.SilverMopper -> R.string.loyalty_tier_silver_mopper
    LoyaltyTier.GoldPolisher -> R.string.loyalty_tier_gold_polisher
    LoyaltyTier.PlatinumSparkler -> R.string.loyalty_tier_platinum_sparkler
}

@Composable
private fun HeroCard(
    tier: LoyaltyTier,
    lifetimePoints: Int,
    bookingsCount: Int,
) {
    val gradient = tierGradientColors(tier)
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(22.dp))
            .background(Brush.linearGradient(listOf(gradient.first, gradient.second)))
            .padding(20.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(64.dp)
                    .background(Color.White.copy(alpha = 0.22f), CircleShape),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    tierIcon(tier),
                    contentDescription = null,
                    tint = Color.White,
                    modifier = Modifier.size(36.dp),
                )
            }
            Spacer(Modifier.width(16.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    stringResource(tierLabelRes(tier)),
                    style = MaterialTheme.typography.titleLarge.copy(
                        fontFamily = Poppins,
                        fontWeight = FontWeight.Bold,
                    ),
                    color = Color.White,
                )
                Spacer(Modifier.height(2.dp))
                Text(
                    stringResource(R.string.loyalty_lifetime_points),
                    style = MaterialTheme.typography.labelMedium,
                    color = Color.White.copy(alpha = 0.85f),
                )
            }
        }
        Spacer(Modifier.height(18.dp))
        Row(verticalAlignment = Alignment.Bottom) {
            Text(
                lifetimePoints.toString(),
                style = MaterialTheme.typography.displayMedium.copy(
                    fontFamily = Poppins,
                    fontWeight = FontWeight.Bold,
                ),
                color = Color.White,
            )
            Spacer(Modifier.width(8.dp))
            Text(
                stringResource(R.string.loyalty_points_unit),
                style = MaterialTheme.typography.titleMedium,
                color = Color.White.copy(alpha = 0.9f),
                modifier = Modifier.padding(bottom = 10.dp),
            )
        }
        Spacer(Modifier.height(4.dp))
        Text(
            stringResource(R.string.loyalty_bookings_completed, bookingsCount),
            style = MaterialTheme.typography.bodyMedium,
            color = Color.White.copy(alpha = 0.85f),
        )
    }
}

/* ── Progress ── */

@Composable
private fun ProgressCard(
    account: LoyaltyAccountDto,
    currentTier: LoyaltyTier,
) {
    val pointsToNext = account.pointsToNextTier
    val nextTier = LoyaltyTier.fromValue(account.nextTier)

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(18.dp))
            .padding(16.dp),
    ) {
        if (pointsToNext == null || nextTier == null) {
            // Platinum reached — celebratory single-line. We still render the
            // card so the section spacing stays consistent across tiers.
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    Icons.Outlined.Star,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(20.dp),
                )
                Spacer(Modifier.width(8.dp))
                Text(
                    stringResource(R.string.loyalty_max_tier_reached),
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
        } else {
            // pointsToNext = nextThreshold - lifetimePoints, so:
            //   nextThreshold = lifetimePoints + pointsToNext
            //   progress = lifetimePoints / nextThreshold
            val nextThreshold = account.lifetimePoints + pointsToNext
            val progress = if (nextThreshold > 0) {
                (account.lifetimePoints.toFloat() / nextThreshold.toFloat()).coerceIn(0f, 1f)
            } else 0f
            Text(
                stringResource(
                    R.string.loyalty_progress_to_next,
                    account.lifetimePoints,
                    nextThreshold,
                    stringResource(tierLabelRes(nextTier)),
                ),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
            )
            Spacer(Modifier.height(10.dp))
            LinearProgressIndicator(
                progress = { progress },
                modifier = Modifier
                    .fillMaxWidth()
                    .height(8.dp)
                    .clip(RoundedCornerShape(4.dp)),
                color = MaterialTheme.colorScheme.primary,
                trackColor = MaterialTheme.colorScheme.surfaceVariant,
            )
        }
    }
}

/* ── Current perks ── */

@Composable
private fun CurrentPerksCard(perks: List<TierPerkDto>) {
    // Bronze with no backend-supplied perks falls back to the default welcome
    // badge so the section never renders empty (would look broken).
    val effective = if (perks.isEmpty()) {
        listOf(TierPerkDto(icon = "badge", labelKey = "loyalty.perks.welcome_badge"))
    } else perks

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(18.dp))
            .padding(16.dp),
    ) {
        Text(
            stringResource(R.string.loyalty_current_perks_title),
            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onBackground,
        )
        Spacer(Modifier.height(12.dp))
        effective.forEachIndexed { idx, perk ->
            PerkRow(perk)
            if (idx < effective.lastIndex) Spacer(Modifier.height(10.dp))
        }
    }
}

@Composable
private fun PerkRow(perk: TierPerkDto) {
    val context = LocalContext.current
    val resolved = remember(perk.labelKey) { resolveLabelKey(context, perk.labelKey) }
    Row(verticalAlignment = Alignment.CenterVertically) {
        Box(
            modifier = Modifier
                .size(32.dp)
                .background(
                    MaterialTheme.colorScheme.primary.copy(alpha = 0.12f),
                    CircleShape,
                ),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.CheckCircle,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(18.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Text(
            resolved,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

/**
 * Backend perk label keys use dot notation (`loyalty.perks.welcome_badge`).
 * Android resource names must be underscores (`loyalty_perks_welcome_badge`).
 * Falls back to the raw key if not found so unknown future perks stay visible
 * (better than silently dropping them).
 */
private fun resolveLabelKey(
    context: android.content.Context,
    labelKey: String?,
): String {
    if (labelKey.isNullOrBlank()) return ""
    val resName = labelKey.replace('.', '_')
    val resId = context.resources.getIdentifier(resName, "string", context.packageName)
    return if (resId != 0) context.getString(resId) else labelKey
}

/* ── Tier ladder ── */

@Composable
private fun TierLadderCard(
    tiers: List<TierInfoDto>,
    currentTier: LoyaltyTier,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(18.dp))
            .padding(16.dp),
    ) {
        Text(
            stringResource(R.string.loyalty_tier_ladder_title),
            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onBackground,
        )
        Spacer(Modifier.height(12.dp))

        // Sort by tier value so the ladder renders Bronze → Platinum top-down
        // even if the backend payload arrives in arbitrary order.
        val sorted = tiers.sortedBy { it.tier }
        if (sorted.isEmpty()) {
            // Fallback for the rare case where the tiers fetch failed but the
            // account fetch succeeded. Render a muted note rather than an
            // empty card so the user knows something's missing.
            Text(
                stringResource(R.string.loyalty_error_load),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        } else {
            sorted.forEachIndexed { idx, tier ->
                TierLadderRow(tierDto = tier, currentTier = currentTier)
                if (idx < sorted.lastIndex) Spacer(Modifier.height(12.dp))
            }
        }
    }
}

@Composable
private fun TierLadderRow(
    tierDto: TierInfoDto,
    currentTier: LoyaltyTier,
) {
    val tier = LoyaltyTier.fromValue(tierDto.tier) ?: return
    val gradient = tierGradientColors(tier)
    val isCurrent = tier.value == currentTier.value
    val isUnlocked = tier.value < currentTier.value
    val isLocked = tier.value > currentTier.value

    Row(verticalAlignment = Alignment.CenterVertically) {
        Box(
            modifier = Modifier
                .size(40.dp)
                .background(
                    Brush.linearGradient(listOf(gradient.first, gradient.second)),
                    CircleShape,
                ),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                tierIcon(tier),
                contentDescription = null,
                tint = Color.White,
                modifier = Modifier.size(22.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    stringResource(tierLabelRes(tier)),
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                Spacer(Modifier.width(6.dp))
                Text(
                    "·",
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Spacer(Modifier.width(6.dp))
                Text(
                    stringResource(R.string.loyalty_threshold_points, tierDto.lifetimePointsThreshold),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Text(
                composeDiscountSummary(tierDto),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        when {
            isCurrent -> StatusPill(
                label = stringResource(R.string.loyalty_tier_status_current),
                color = MaterialTheme.colorScheme.primary,
            )
            isUnlocked -> Icon(
                Icons.Outlined.CheckCircle,
                contentDescription = stringResource(R.string.loyalty_tier_status_unlocked),
                tint = SuccessText,
                modifier = Modifier.size(22.dp),
            )
            isLocked -> Icon(
                Icons.Outlined.Lock,
                contentDescription = stringResource(R.string.loyalty_tier_status_locked),
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp),
            )
        }
    }
}

/**
 * Compose the per-tier discount summary line shown under the tier name in the
 * ladder. Three branches:
 *  - 0% discount → "No discount yet" (Bronze)
 *  - >0% with min order amount → "X% off orders ≥Y CZK" (Silver)
 *  - >0% with no min → "X% off all bookings" (Gold / Platinum)
 *
 * Discount percent is rendered as an integer (5, 10, 15) — backend stores it
 * as a 0..1 decimal so we multiply and round. Min-order amount likewise rounded
 * to a whole CZK value (no fractional thresholds exist in the agreed config).
 */
@Composable
private fun composeDiscountSummary(tierDto: TierInfoDto): String {
    val pct = (tierDto.discountPercent * 100).toInt()
    if (pct <= 0) return stringResource(R.string.loyalty_no_discount_yet)
    val minOrder = tierDto.minimumOrderAmountForDiscount?.toInt() ?: 0
    return if (minOrder > 0) {
        stringResource(R.string.loyalty_discount_min_order, pct, minOrder)
    } else {
        stringResource(R.string.loyalty_discount_basic, pct)
    }
}

@Composable
private fun StatusPill(label: String, color: Color) {
    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(999.dp))
            .background(color.copy(alpha = 0.14f))
            .padding(horizontal = 10.dp, vertical = 4.dp),
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = color,
        )
    }
}

/* ── Activity preview ── */

@Composable
private fun ActivityPreviewCard(
    activity: List<LoyaltyActivityItemDto>,
    onOpenActivity: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(18.dp))
            .padding(16.dp),
    ) {
        Text(
            stringResource(R.string.loyalty_activity_title),
            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onBackground,
        )
        Spacer(Modifier.height(12.dp))
        if (activity.isEmpty()) {
            Text(
                stringResource(R.string.loyalty_empty_activity),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        } else {
            activity.forEachIndexed { idx, item ->
                ActivityRow(item)
                if (idx < activity.lastIndex) Spacer(Modifier.height(10.dp))
            }
            Spacer(Modifier.height(12.dp))
            Text(
                stringResource(R.string.loyalty_activity_view_all),
                style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.primary,
                modifier = Modifier
                    .clip(RoundedCornerShape(8.dp))
                    .clickable(onClick = onOpenActivity)
                    .padding(vertical = 6.dp),
            )
        }
    }
}

/**
 * Single transaction row — shared between the inline preview and the full
 * activity list (kept internal here for the preview; the activity screen
 * has its own copy specialised for LazyColumn).
 */
@Composable
internal fun ActivityRow(item: LoyaltyActivityItemDto) {
    val type = LoyaltyTransactionType.fromValue(item.type)
    val source = LoyaltyEarnSource.fromValue(item.source)
    val isPositive = item.points >= 0
    val pointsColor = if (isPositive) SuccessText else MaterialTheme.colorScheme.error

    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = transactionDescription(type, source, item),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                text = formatOrderDateTime(item.occurredOn),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Text(
            text = if (isPositive) "+${item.points}" else item.points.toString(),
            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
            color = pointsColor,
        )
    }
}

/**
 * Pick the right description string for a transaction based on its type +
 * source. Order-linked transactions try to embed the display order number; if
 * absent (e.g. a manual grant or a referral) we fall back to a no-args variant.
 */
@Composable
private fun transactionDescription(
    type: LoyaltyTransactionType?,
    source: LoyaltyEarnSource?,
    item: LoyaltyActivityItemDto,
): String {
    val orderRef = item.orderDisplayNumber?.takeIf { it.isNotBlank() } ?: "—"
    val signedPoints = item.points
    return when (source) {
        LoyaltyEarnSource.OrderCompleted -> stringResource(
            R.string.loyalty_tx_earn_order, signedPoints, orderRef,
        )
        LoyaltyEarnSource.OrderCancelled -> stringResource(
            R.string.loyalty_tx_revoke_order, signedPoints, orderRef,
        )
        LoyaltyEarnSource.Referral -> stringResource(
            R.string.loyalty_tx_referral, signedPoints,
        )
        LoyaltyEarnSource.ManualGrant -> stringResource(
            R.string.loyalty_tx_manual, signedPoints,
        )
        // Unknown source from a future backend addition — fall back to a
        // best-effort line that at least surfaces the points delta.
        null -> stringResource(R.string.loyalty_tx_manual, signedPoints)
    }
}

/* ── Invite friends card (Loyalty Phase C) ── */

/**
 * Renders the user's referral code with copy + share affordances and the
 * stats line. Stats text picks one of three variants by counter shape:
 *  - 0 accepted ⇒ `loyalty_referral_stats_empty`
 *  - >0 accepted but 0 qualified ⇒ `loyalty_referral_stats_waiting`
 *  - >0 qualified ⇒ `loyalty_referral_stats_qualified` (joined · qualified)
 *
 * Long-press on the code badge copies to clipboard. The "Share" button
 * opens the system chooser; if no share target is installed (rare on
 * stripped-down ROMs), it falls back to clipboard copy with a snackbar.
 */
@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun InviteFriendsCard(referral: ReferralAccountDto) {
    val context = LocalContext.current
    // TODO(W3.3): refactor to VM injection — leaf composable; lift snackbar
    // through parent RewardsTab once it grows a VM.
    val snackbar = remember {
        EntryPointAccessors
            .fromApplication(context, SnackbarControllerEntryPoint::class.java)
            .snackbarController()
    }
    val code = referral.code

    val statsLine: String = when {
        referral.acceptedCount <= 0 ->
            stringResource(R.string.loyalty_referral_stats_empty)
        referral.qualifiedCount <= 0 ->
            stringResource(R.string.loyalty_referral_stats_waiting, referral.acceptedCount)
        else ->
            stringResource(
                R.string.loyalty_referral_stats_qualified,
                referral.acceptedCount,
                referral.qualifiedCount,
            )
    }

    val onCopy: () -> Unit = {
        copyToClipboard(context, code)
        snackbar.showSuccess(context.getString(R.string.loyalty_referral_copied_toast))
    }

    val onShare: () -> Unit = {
        shareReferralOrFallback(context, code, snackbar)
    }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(18.dp))
            .padding(18.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(36.dp)
                    .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.14f), CircleShape),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    Icons.Outlined.CardGiftcard,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(20.dp),
                )
            }
            Spacer(Modifier.width(12.dp))
            Text(
                stringResource(R.string.loyalty_referral_section_title),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onBackground,
                modifier = Modifier.weight(1f),
            )
        }
        Spacer(Modifier.height(8.dp))
        Text(
            stringResource(R.string.loyalty_referral_subtitle),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(16.dp))

        // Code badge — long-press to copy. Visually distinct (primary tint
        // background, monospaced if available) so it reads as "the artifact".
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Box(
                modifier = Modifier
                    .weight(1f)
                    .clip(RoundedCornerShape(12.dp))
                    .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.10f))
                    .border(
                        1.dp,
                        MaterialTheme.colorScheme.primary.copy(alpha = 0.30f),
                        RoundedCornerShape(12.dp),
                    )
                    .combinedClickable(onClick = onCopy, onLongClick = onCopy)
                    .padding(vertical = 16.dp, horizontal = 16.dp),
                contentAlignment = Alignment.Center,
            ) {
                Text(
                    text = code,
                    style = MaterialTheme.typography.headlineSmall.copy(
                        fontFamily = androidx.compose.ui.text.font.FontFamily.Monospace,
                        fontWeight = FontWeight.Bold,
                        letterSpacing = androidx.compose.ui.unit.TextUnit(2f, androidx.compose.ui.unit.TextUnitType.Sp),
                    ),
                    color = MaterialTheme.colorScheme.primary,
                )
            }
            Spacer(Modifier.width(10.dp))
            Box(
                modifier = Modifier
                    .size(48.dp)
                    .clip(RoundedCornerShape(12.dp))
                    .background(MaterialTheme.colorScheme.surfaceVariant)
                    .clickable(onClick = onCopy),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    Icons.Outlined.ContentCopy,
                    contentDescription = stringResource(R.string.loyalty_referral_copy_button),
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(20.dp),
                )
            }
        }
        Spacer(Modifier.height(12.dp))
        Text(
            statsLine,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(14.dp))

        // Full-width share CTA. Primary container so it pops against the card
        // surface without competing with the loyalty hero gradient above.
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(14.dp))
                .background(MaterialTheme.colorScheme.primary)
                .clickable(onClick = onShare)
                .padding(vertical = 14.dp),
            horizontalArrangement = Arrangement.Center,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                Icons.Outlined.Share,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onPrimary,
                modifier = Modifier.size(18.dp),
            )
            Spacer(Modifier.width(8.dp))
            Text(
                stringResource(R.string.loyalty_referral_share_button),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onPrimary,
            )
        }
    }
}

/** Copies the referral code to the system clipboard. */
private fun copyToClipboard(context: android.content.Context, code: String) {
    val clipboard = context.getSystemService(android.content.Context.CLIPBOARD_SERVICE)
        as? android.content.ClipboardManager
    clipboard?.setPrimaryClip(android.content.ClipData.newPlainText("referral_code", code))
}

/**
 * Launches the system share sheet. Falls back to clipboard + snackbar when no
 * activity can handle ACTION_SEND (rare — stripped-down devices, headless tests).
 */
private fun shareReferralOrFallback(
    context: android.content.Context,
    code: String,
    snackbar: SnackbarController,
) {
    val landingUrl = "https://cleansia.cz/r/$code"
    val message = context.getString(R.string.loyalty_referral_share_text, code, landingUrl)
    val send = android.content.Intent(android.content.Intent.ACTION_SEND).apply {
        type = "text/plain"
        putExtra(android.content.Intent.EXTRA_TEXT, message)
    }
    val chooser = android.content.Intent.createChooser(
        send,
        context.getString(R.string.loyalty_referral_share_button),
    )
    try {
        // FLAG_ACTIVITY_NEW_TASK because we may be invoked from a non-Activity
        // context in some host configurations.
        chooser.flags = chooser.flags or android.content.Intent.FLAG_ACTIVITY_NEW_TASK
        context.startActivity(chooser)
    } catch (e: android.content.ActivityNotFoundException) {
        copyToClipboard(context, code)
        snackbar.showInfo(context.getString(R.string.loyalty_referral_share_failed))
    }
}

/* ── States ── */

@Composable
private fun LoyaltyLoading() {
    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.Center,
    ) {
        CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
    }
}

@Composable
private fun LoyaltyError(onRetry: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(40.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            Icons.Outlined.CloudOff,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(40.dp),
        )
        Spacer(Modifier.height(12.dp))
        Text(
            stringResource(R.string.loyalty_error_load),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(12.dp))
        Text(
            stringResource(R.string.loyalty_retry),
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier
                .clip(RoundedCornerShape(999.dp))
                .clickable(onClick = onRetry)
                .padding(horizontal = 16.dp, vertical = 8.dp),
        )
    }
}

/**
 * Wraps an otherwise-static state composable in a verticalScroll so
 * PullToRefreshBox has a scrollable child to attach the gesture to. Same
 * trick the orders tab uses for its empty/error mascots.
 */
@Composable
private fun ScrollableStateContainer(content: @Composable () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState()),
    ) {
        content()
    }
}
