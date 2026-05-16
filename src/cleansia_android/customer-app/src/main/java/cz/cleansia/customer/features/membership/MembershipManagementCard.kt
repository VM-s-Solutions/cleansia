package cz.cleansia.customer.features.membership

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowForward
import androidx.compose.material.icons.outlined.Autorenew
import androidx.compose.material.icons.outlined.Bolt
import androidx.compose.material.icons.outlined.EventBusy
import androidx.compose.material.icons.outlined.LocalOffer
import androidx.compose.material.icons.outlined.Repeat
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material.icons.outlined.WorkspacePremium
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.customer.R
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.EntryPointAccessors
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

/**
 * Membership card rendered on the Profile tab. Three states:
 *  - **No active membership**: shows the marketing card with "Try Plus" CTA
 *    that navigates to [SubscribePlusScreen].
 *  - **Active**: shows plan name, perks summary, period end, "Cancel" button.
 *  - **Cancellation requested**: shows "Active until {date}, then ends" copy
 *    and hides the cancel button (Stripe handles the actual cancel via the
 *    customer.subscription.deleted webhook at period end).
 *
 * Refreshes on first composition; the underlying repository cache means
 * navigating away + back doesn't always re-fetch.
 */
@Composable
fun MembershipManagementCard(
    modifier: Modifier = Modifier,
    onSubscribeClick: () -> Unit,
    viewModel: MembershipViewModel = hiltViewModel(),
) {
    val current by viewModel.current.collectAsState()
    val plans by viewModel.plans.collectAsState()
    val submitting by viewModel.submitting.collectAsState()
    val context = LocalContext.current

    // TODO(W3.3): refactor to VM injection — pull snackbar into
    // MembershipViewModel like ProfileViewModel/OrderDetailViewModel.
    val snackbar = remember {
        EntryPointAccessors
            .fromApplication(context, SubscribePlusEntryPoint::class.java)
            .snackbarController()
    }

    var showCancelDialog by remember { mutableStateOf(false) }
    var showSwitchDialog by remember { mutableStateOf(false) }

    // Find the yearly plan if there is one — drives the "Switch to annual" CTA
    // visibility. Only rendered when the user is on Monthly + a Yearly plan
    // exists in the catalog.
    val yearlyPlan = remember(plans) {
        plans.firstOrNull { it.billingInterval == 2 }
    }
    val membership = current
    val showSwitchCta = membership?.hasMembership == true &&
        membership.billingInterval == 1 &&
        !membership.cancelRequested &&
        yearlyPlan != null

    when {
        membership == null -> Unit  // first load — render nothing rather than flash
        !membership.hasMembership -> InactiveCard(modifier = modifier, onClick = onSubscribeClick)
        else -> ActiveCard(
            modifier = modifier,
            response = membership,
            onCancelClick = { showCancelDialog = true },
            cancelEnabled = !submitting && !membership.cancelRequested,
            onSwitchToAnnualClick = if (showSwitchCta) ({ showSwitchDialog = true }) else null,
            yearlyPlan = yearlyPlan,
        )
    }

    if (showCancelDialog) {
        CleansiaDialog(
            onDismiss = { showCancelDialog = false },
            title = stringResource(R.string.membership_cancel_dialog_title),
            message = stringResource(R.string.membership_cancel_dialog_message),
            destructive = true,
            confirmLabel = stringResource(R.string.membership_cancel_dialog_confirm),
            onConfirm = {
                showCancelDialog = false
                viewModel.cancel { effectiveDate ->
                    snackbar.showSuccess(
                        context.getString(
                            R.string.membership_cancelled_until,
                            formatPeriodEnd(effectiveDate),
                        ),
                    )
                }
            },
            dismissLabel = stringResource(R.string.common_back),
        )
    }

    if (showSwitchDialog && yearlyPlan != null) {
        CleansiaDialog(
            onDismiss = { showSwitchDialog = false },
            title = stringResource(R.string.membership_switch_dialog_title),
            message = stringResource(
                R.string.membership_switch_dialog_message,
                formatPriceCzkCard(yearlyPlan.price),
            ),
            confirmLabel = stringResource(R.string.membership_switch_dialog_confirm),
            onConfirm = {
                showSwitchDialog = false
                viewModel.swapPlan(yearlyPlan.code) {
                    snackbar.showSuccess(context.getString(R.string.membership_switch_success))
                }
            },
            dismissLabel = stringResource(R.string.common_back),
        )
    }
}

/**
 * Inactive (not-subscribed) Plus card — restructured into a richer
 * marketing block. Layout:
 *
 *   ┌────────────────────────────────────────────────────────┐
 *   │  ◉ PLUS BADGE                              🧽 mascot   │
 *   │  Cleansia Plus                                         │
 *   │  Save 10% · cancel anytime · recurring · …             │
 *   │  ─────────────────────────────────────                 │
 *   │  [ Try Plus free for 14 days  ▸ ]                      │
 *   └────────────────────────────────────────────────────────┘
 *
 * The mascot anchors the brand emotionally without taking over; the
 * single-line perks summary teases value before the user has to commit
 * to a tap; the explicit CTA button reads as an affordance even on
 * users who skim past the gradient background.
 */
@Composable
private fun InactiveCard(modifier: Modifier, onClick: () -> Unit) {
    val cardShape = RoundedCornerShape(20.dp)
    Column(
        modifier = modifier
            .fillMaxWidth()
            .clip(cardShape)
            .background(
                Brush.horizontalGradient(
                    listOf(
                        MaterialTheme.colorScheme.primary.copy(alpha = 0.14f),
                        MaterialTheme.colorScheme.primary.copy(alpha = 0.04f),
                    ),
                ),
            )
            .border(
                1.dp,
                MaterialTheme.colorScheme.primary.copy(alpha = 0.35f),
                cardShape,
            )
            .clickable(onClick = onClick),
    ) {
        // ── Header row — badge pill (left) + mascot (right) ──
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 14.dp),
            verticalAlignment = Alignment.Top,
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Row(
                    modifier = Modifier
                        .clip(RoundedCornerShape(8.dp))
                        .background(MaterialTheme.colorScheme.primary)
                        .padding(horizontal = 8.dp, vertical = 4.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Icon(
                        Icons.Outlined.WorkspacePremium,
                        contentDescription = null,
                        tint = androidx.compose.ui.graphics.Color.White,
                        modifier = Modifier.size(14.dp),
                    )
                    Spacer(Modifier.width(4.dp))
                    Text(
                        text = stringResource(R.string.membership_inactive_badge),
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                        color = androidx.compose.ui.graphics.Color.White,
                    )
                }
                Spacer(Modifier.height(8.dp))
                Text(
                    text = stringResource(R.string.membership_inactive_title),
                    style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                Spacer(Modifier.height(2.dp))
                Text(
                    text = stringResource(R.string.membership_inactive_perks_summary),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Spacer(Modifier.width(8.dp))
            // Mascot — "ready to clean" persona; signals the perk you're
            // about to unlock without burning the cleaner-themed mascots
            // we save for booking moments.
            androidx.compose.foundation.Image(
                painter = androidx.compose.ui.res.painterResource(R.drawable.mascot_ready),
                contentDescription = null,
                modifier = Modifier.size(72.dp),
            )
        }

        // ── CTA row ──
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 12.dp)
                .clip(RoundedCornerShape(12.dp))
                .background(MaterialTheme.colorScheme.primary)
                .padding(horizontal = 14.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text(
                text = stringResource(R.string.membership_inactive_cta),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                color = androidx.compose.ui.graphics.Color.White,
                modifier = Modifier.weight(1f),
            )
            Icon(
                Icons.AutoMirrored.Outlined.ArrowForward,
                contentDescription = null,
                tint = androidx.compose.ui.graphics.Color.White,
                modifier = Modifier.size(20.dp),
            )
        }
    }
}

/**
 * Active membership card — premium-feeling layout with mascot anchor,
 * gradient header, perk-pill row, period info, and action footer. Two
 * color treatments:
 *
 *  - **Renewing** (default): primary blue gradient + "Active" badge.
 *  - **Cancellation requested**: amber gradient + "ENDING" badge with
 *    EventBusy icon — communicates "this WILL end" without scolding.
 */
@OptIn(androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
@Composable
private fun ActiveCard(
    modifier: Modifier,
    response: cz.cleansia.customer.core.memberships.GetMyMembershipResponse,
    onCancelClick: () -> Unit,
    cancelEnabled: Boolean,
    onSwitchToAnnualClick: (() -> Unit)?,
    yearlyPlan: cz.cleansia.customer.core.memberships.MembershipPlanDto?,
) {
    val cardShape = RoundedCornerShape(20.dp)
    val isCancelling = response.cancelRequested
    // Premium gold for the active+renewing state — feels rewarding, marks
    // membership as a status. Distinct desaturated red-slate for the
    // cancellation-requested state — communicates "winding down" without
    // looking like an upgrade. The active treatment SHOULD feel richer
    // than the ending one (otherwise cancelling looks like a perk).
    val accent = if (isCancelling) EndingAccent else PremiumGold
    val periodEndText = response.currentPeriodEnd?.let { formatPeriodEnd(it) }

    Column(
        modifier = modifier
            .fillMaxWidth()
            .clip(cardShape)
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, accent.copy(alpha = 0.35f), cardShape),
    ) {
        // ── Header strip — gradient + plan name + status badge + mascot anchor ──
        val headerBrush = Brush.horizontalGradient(
            listOf(
                accent.copy(alpha = 0.22f),
                accent.copy(alpha = 0.06f),
            ),
        )
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .background(headerBrush)
                .padding(start = 16.dp, end = 12.dp, top = 14.dp, bottom = 14.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        Icons.Outlined.WorkspacePremium,
                        contentDescription = null,
                        tint = accent,
                        modifier = Modifier.size(18.dp),
                    )
                    Spacer(Modifier.width(6.dp))
                    Text(
                        text = stringResource(
                            if (isCancelling) R.string.membership_status_ending_badge
                            else R.string.membership_status_active_badge,
                        ),
                        style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                        color = accent,
                        modifier = Modifier
                            .clip(RoundedCornerShape(8.dp))
                            .background(accent.copy(alpha = 0.15f))
                            .padding(horizontal = 8.dp, vertical = 3.dp),
                    )
                }
                Spacer(Modifier.height(6.dp))
                Text(
                    text = response.planName ?: stringResource(R.string.membership_plus_title),
                    style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
            Spacer(Modifier.width(8.dp))
            // Mascot — same "ready" persona used on the inactive card. Anchors
            // the brand emotionally; small enough that it doesn't compete
            // with the textual content.
            androidx.compose.foundation.Image(
                painter = androidx.compose.ui.res.painterResource(R.drawable.mascot_ready),
                contentDescription = null,
                modifier = Modifier.size(64.dp),
            )
        }

        // ── Perk pill row — quick visual reminder of what's unlocked ──
        val perks = remember(response) {
            buildList {
                val pct = response.discountPercentage?.toInt()
                if (pct != null && pct > 0) {
                    add(PerkPill(Icons.Outlined.LocalOffer, "$pct% off"))
                }
                val freeHours = response.freeCancellationWindowHours
                if (freeHours != null && freeHours > 0) {
                    add(PerkPill(Icons.Outlined.Schedule, "${freeHours}h cancel"))
                }
                if (response.allowsExpressUpgrade == true) {
                    add(PerkPill(Icons.Outlined.Bolt, "Express"))
                }
                add(PerkPill(Icons.Outlined.Repeat, "Recurring"))
            }
        }
        if (perks.isNotEmpty()) {
            androidx.compose.foundation.layout.FlowRow(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp, vertical = 12.dp),
                horizontalArrangement = Arrangement.spacedBy(6.dp),
                verticalArrangement = Arrangement.spacedBy(6.dp),
            ) {
                perks.forEach { perk ->
                    Row(
                        modifier = Modifier
                            .clip(RoundedCornerShape(999.dp))
                            .background(accent.copy(alpha = 0.10f))
                            .padding(horizontal = 10.dp, vertical = 6.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Icon(
                            imageVector = perk.icon,
                            contentDescription = null,
                            tint = accent,
                            modifier = Modifier.size(14.dp),
                        )
                        Spacer(Modifier.width(4.dp))
                        Text(
                            text = perk.label,
                            style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.SemiBold),
                            color = MaterialTheme.colorScheme.onSurface,
                        )
                    }
                }
            }
        }

        androidx.compose.material3.HorizontalDivider(
            color = MaterialTheme.colorScheme.outlineVariant,
            thickness = 1.dp,
        )

        // ── Period row + switch-to-annual + cancel action ──
        Column(modifier = Modifier.padding(horizontal = 16.dp, vertical = 14.dp)) {
            if (periodEndText != null) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = if (isCancelling) Icons.Outlined.EventBusy
                            else Icons.Outlined.Autorenew,
                        contentDescription = null,
                        tint = accent,
                        modifier = Modifier.size(20.dp),
                    )
                    Spacer(Modifier.width(10.dp))
                    Column {
                        Text(
                            text = stringResource(
                                if (isCancelling) R.string.membership_active_until
                                else R.string.membership_renews_on,
                                periodEndText,
                            ),
                            style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                            color = MaterialTheme.colorScheme.onSurface,
                        )
                        Text(
                            text = stringResource(
                                if (isCancelling) R.string.membership_then_ends_hint
                                else R.string.membership_auto_renew_hint,
                            ),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
            }

            if (onSwitchToAnnualClick != null && yearlyPlan != null) {
                Spacer(Modifier.height(14.dp))
                androidx.compose.material3.FilledTonalButton(
                    onClick = onSwitchToAnnualClick,
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    Text(
                        text = stringResource(
                            R.string.membership_switch_to_annual_cta,
                            yearlyPlan.savingsPercentVsMonthly.toInt(),
                        ),
                    )
                }
            }

            if (cancelEnabled) {
                Spacer(Modifier.height(8.dp))
                TextButton(
                    onClick = onCancelClick,
                    modifier = Modifier
                        .align(Alignment.CenterHorizontally)
                        .height(36.dp),
                ) {
                    Text(
                        text = stringResource(R.string.membership_cancel_action),
                        color = MaterialTheme.colorScheme.error,
                    )
                }
            }
        }
    }
}

private data class PerkPill(
    val icon: androidx.compose.ui.graphics.vector.ImageVector,
    val label: String,
)

/**
 * Premium gold accent — drives the active+renewing membership card. Warmer
 * than primary blue, reads as "status / valuable" rather than "default UI".
 * Used for header gradient, perk pills, badge text, and the auto-renew icon.
 */
private val PremiumGold = androidx.compose.ui.graphics.Color(0xFFD97706)

/**
 * Cancellation-requested accent — desaturated rose/red. Visually distinct
 * from the gold active state so users understand at a glance which mode
 * they're in. Pairs with the ENDING badge and EventBusy icon.
 */
private val EndingAccent = androidx.compose.ui.graphics.Color(0xFFB91C1C)

/** CZK formatter shared with the subscribe screen — local copy to avoid file deps. */
private fun formatPriceCzkCard(amount: Double): String {
    val rounded = if (amount % 1.0 == 0.0) amount.toInt().toString() else "%.2f".format(amount)
    return "$rounded Kč"
}

/** Format a backend ISO-8601 instant as a localized short date (e.g. "May 30, 2026"). */
private fun formatPeriodEnd(iso: String): String {
    return runCatching {
        val instant = Instant.parse(iso)
        val formatter = DateTimeFormatter
            .ofPattern("MMM d, yyyy", Locale.getDefault())
            .withZone(ZoneId.systemDefault())
        formatter.format(instant)
    }.getOrDefault(iso)
}
