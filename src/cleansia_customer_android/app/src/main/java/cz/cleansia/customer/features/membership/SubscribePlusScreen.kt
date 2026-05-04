package cz.cleansia.customer.features.membership

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
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
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Bolt
import androidx.compose.material.icons.outlined.CalendarMonth
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.LocalOffer
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Repeat
import androidx.compose.material.icons.outlined.TrendingUp
import androidx.compose.material.icons.outlined.WorkspacePremium
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
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
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.stripe.android.paymentsheet.PaymentSheet
import com.stripe.android.paymentsheet.PaymentSheetResult
import com.stripe.android.paymentsheet.rememberPaymentSheet
import cz.cleansia.customer.BuildConfig
import cz.cleansia.customer.R
import cz.cleansia.customer.ui.snackbar.SnackbarController
import cz.cleansia.customer.ui.theme.Sky400
import cz.cleansia.customer.ui.theme.Sky950
import cz.cleansia.customer.ui.theme.Slate900
import dagger.hilt.android.EntryPointAccessors
import kotlinx.coroutines.launch

/**
 * Cleansia Plus subscribe page — premium-feel marketing page modeled after
 * Bolt Plus / Wolt+:
 *  - Dark hero with brand splash, struck-through old price + bold "0 Kč first
 *    14 days" anchor.
 *  - Plan toggle (monthly / annual) inside the hero so the user picks before
 *    committing.
 *  - Social-proof tile right under the hero ("Members typically save…").
 *  - Perks rendered as tiles with bigger icons; ordered economic-value first
 *    (discount → cancellation → favorite cleaner → recurring → express upgrade).
 *  - Sticky bottom CTA on a contrasting bg with verb-led label
 *    ("Start free trial") + fine-print renewal terms.
 *
 * The two-phase Stripe flow is unchanged from the previous version.
 */
@Composable
fun SubscribePlusScreen(
    onBack: () -> Unit,
    onSubscribed: () -> Unit,
    viewModel: MembershipViewModel = hiltViewModel(),
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val submitting by viewModel.submitting.collectAsState()
    val current by viewModel.current.collectAsState()
    val plans by viewModel.plans.collectAsState()

    var selectedPlanCode by remember(plans) {
        mutableStateOf(plans.firstOrNull { it.billingInterval == 1 }?.code ?: plans.firstOrNull()?.code.orEmpty())
    }
    val selectedPlan = remember(plans, selectedPlanCode) { plans.firstOrNull { it.code == selectedPlanCode } }

    val snackbar = remember {
        EntryPointAccessors
            .fromApplication(context, SubscribePlusEntryPoint::class.java)
            .snackbarController()
    }
    // Guards the post-purchase nav so it only fires once even when both the
    // PaymentSheet result handler AND the membership-state LaunchedEffect
    // observe success. Without it the user can briefly bounce out of the
    // success screen as the auto-back effect re-fires.
    var navigatedAway by remember { mutableStateOf(false) }

    val paymentSheet = rememberPaymentSheet { result ->
        when (result) {
            is PaymentSheetResult.Completed -> {
                scope.launch {
                    val outcome = viewModel.confirmSubscribe(selectedPlanCode)
                    when (outcome) {
                        is SubscribeOutcome.Subscribed -> {
                            // No snackbar — the dedicated success screen IS
                            // the affirmation; doubling up would feel noisy.
                            if (!navigatedAway) {
                                navigatedAway = true
                                onSubscribed()
                            }
                        }
                        else -> Unit // VM already snackbarred
                    }
                }
            }
            is PaymentSheetResult.Canceled -> {
                snackbar.showError(context.getString(R.string.error_payment_cancelled))
            }
            is PaymentSheetResult.Failed -> {
                snackbar.showError(
                    result.error.localizedMessage
                        ?: context.getString(R.string.error_payment_failed),
                )
            }
        }
    }

    // If the user landed here while ALREADY subscribed (e.g. opened from a
    // stale link or a back-navigation race), bounce straight to the
    // management view. Guarded by [navigatedAway] so a fresh purchase doesn't
    // trigger this branch — that path goes through onSubscribed() above.
    LaunchedEffect(current?.hasMembership) {
        if (current?.hasMembership == true && !navigatedAway) {
            navigatedAway = true
            onBack()
        }
    }

    Box(modifier = Modifier.fillMaxSize().background(MaterialTheme.colorScheme.background)) {
        // Scrollable content sits behind the sticky CTA bar; we add bottom
        // padding equal to the bar height so the last perk doesn't get hidden.
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(bottom = 140.dp),
        ) {
            HeroBlock(
                onBack = onBack,
                plans = plans,
                selectedPlanCode = selectedPlanCode,
                onSelectPlan = { selectedPlanCode = it },
                selectedPlan = selectedPlan,
            )

            Spacer(Modifier.height(20.dp))

            // Social-proof tile — anchors the value prop in concrete savings.
            // Number is seeded; revisit once we have analytics on actual member
            // discount realization.
            SocialProofTile()

            Spacer(Modifier.height(24.dp))

            PerksHeader()
            Spacer(Modifier.height(12.dp))

            // Order matters: economic value first, niche perks last.
            PerkTile(
                icon = Icons.Outlined.LocalOffer,
                title = stringResource(R.string.membership_perk_discount_title),
                desc = stringResource(R.string.membership_perk_discount_desc),
            )
            Spacer(Modifier.height(10.dp))
            PerkTile(
                icon = Icons.Outlined.CheckCircle,
                title = stringResource(R.string.membership_perk_cancellation_title),
                desc = stringResource(R.string.membership_perk_cancellation_desc),
            )
            Spacer(Modifier.height(10.dp))
            PerkTile(
                icon = Icons.Outlined.Person,
                title = stringResource(R.string.membership_perk_favorite_cleaner_title),
                desc = stringResource(R.string.membership_perk_favorite_cleaner_desc),
            )
            Spacer(Modifier.height(10.dp))
            PerkTile(
                icon = Icons.Outlined.Repeat,
                title = stringResource(R.string.membership_perk_recurring_title),
                desc = stringResource(R.string.membership_perk_recurring_desc),
            )
            Spacer(Modifier.height(10.dp))
            PerkTile(
                icon = Icons.Outlined.Bolt,
                title = stringResource(R.string.membership_perk_express_title),
                desc = stringResource(R.string.membership_perk_express_desc),
            )

            Spacer(Modifier.height(24.dp))
        }

        // Sticky CTA bar — sits above the navigation bar, on a contrasting
        // surface so the button is always visible regardless of scroll position.
        StickyCtaBar(
            modifier = Modifier.align(Alignment.BottomCenter),
            ctaLabel = if ((selectedPlan?.trialPeriodDays ?: 0) > 0) {
                stringResource(R.string.membership_cta_start_trial)
            } else {
                stringResource(R.string.membership_cta_subscribe)
            },
            disclosure = buildDisclosure(selectedPlan),
            enabled = !submitting && selectedPlanCode.isNotBlank(),
            onClick = {
                if (selectedPlanCode.isBlank()) return@StickyCtaBar
                scope.launch {
                    val outcome = viewModel.startSubscribe(selectedPlanCode)
                    when (outcome) {
                        is SubscribeOutcome.NeedsPaymentMethod -> {
                            paymentSheet.presentWithSetupIntent(
                                setupIntentClientSecret = outcome.setupIntentClientSecret,
                                configuration = PaymentSheet.Configuration(
                                    merchantDisplayName = "Cleansia",
                                    customer = PaymentSheet.CustomerConfiguration(
                                        id = outcome.customerId,
                                        ephemeralKeySecret = outcome.ephemeralKey,
                                    ),
                                    googlePay = PaymentSheet.GooglePayConfiguration(
                                        environment = if (BuildConfig.DEBUG) {
                                            PaymentSheet.GooglePayConfiguration.Environment.Test
                                        } else {
                                            PaymentSheet.GooglePayConfiguration.Environment.Production
                                        },
                                        countryCode = "CZ",
                                        currencyCode = "CZK",
                                    ),
                                    allowsDelayedPaymentMethods = false,
                                ),
                            )
                        }
                        SubscribeOutcome.AlreadyActive -> {
                            snackbar.showSuccess(context.getString(R.string.membership_already_active))
                            onBack()
                        }
                        SubscribeOutcome.Failed -> Unit
                        is SubscribeOutcome.Subscribed -> Unit
                    }
                }
            },
        )
    }
}

/**
 * Dark gradient hero with back arrow, brand splash, big trial-first price,
 * and the monthly/annual plan toggle. The trial price is the visual anchor —
 * the "199 Kč" struck-through line under it is doing comparison work, not the
 * other way around.
 */
@Composable
private fun HeroBlock(
    onBack: () -> Unit,
    plans: List<cz.cleansia.customer.core.memberships.MembershipPlanDto>,
    selectedPlanCode: String,
    onSelectPlan: (String) -> Unit,
    selectedPlan: cz.cleansia.customer.core.memberships.MembershipPlanDto?,
) {
    val trialDays = selectedPlan?.trialPeriodDays ?: 0
    // Annual: lead with the year price (no per-month split — keeps pricing
    // honest and frames the "2030 Kč once" commitment up front).
    // Monthly: lead with the per-month price as before.
    val isAnnual = selectedPlan?.billingInterval == 2
    val regularPrice = selectedPlan?.price ?: 0.0
    val regularPriceLabelRes = if (isAnnual) {
        R.string.membership_plan_per_year
    } else {
        R.string.membership_plan_per_month
    }
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .background(
                Brush.verticalGradient(
                    listOf(Sky950, Slate900),
                ),
            )
            .windowInsetsPadding(WindowInsets.statusBars),
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp, vertical = 16.dp),
        ) {
            // Back arrow on white-on-dark for visibility.
            IconButton(
                onClick = onBack,
                modifier = Modifier.size(40.dp),
            ) {
                Icon(
                    Icons.AutoMirrored.Outlined.ArrowBack,
                    contentDescription = null,
                    tint = Color.White,
                )
            }

            Spacer(Modifier.height(8.dp))

            // Brand splash — Plus wordmark + premium glyph. Centered for "logo
            // moment" feel; eyes lock on this first.
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.Center,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    text = "Cleansia",
                    style = MaterialTheme.typography.displaySmall.copy(
                        fontWeight = FontWeight.ExtraBold,
                        fontSize = 36.sp,
                    ),
                    color = Color.White,
                )
                Spacer(Modifier.width(8.dp))
                Box(
                    modifier = Modifier
                        .clip(RoundedCornerShape(10.dp))
                        .background(Sky400)
                        .padding(horizontal = 10.dp, vertical = 4.dp),
                ) {
                    Text(
                        text = "PLUS",
                        style = MaterialTheme.typography.titleMedium.copy(
                            fontWeight = FontWeight.ExtraBold,
                            fontSize = 18.sp,
                        ),
                        color = Slate900,
                    )
                }
            }

            Spacer(Modifier.height(24.dp))

            Text(
                text = stringResource(R.string.membership_hero_headline),
                style = MaterialTheme.typography.headlineMedium.copy(fontWeight = FontWeight.Bold),
                color = Color.White,
                textAlign = TextAlign.Center,
                modifier = Modifier.fillMaxWidth(),
            )

            Spacer(Modifier.height(16.dp))

            // The price block — trial price huge, struck-through regular price
            // small below. Order is intentional: free anchor first, comparison
            // second. If there's no trial, the regular per-month price IS the
            // anchor and the struck line goes away.
            //
            // Sizes intentionally smaller than headline-display defaults so the
            // line "0 Kč / first 14 days" stays on a single line on narrow
            // phones (~360dp). 36sp is the upper bound that still fits.
            if (trialDays > 0) {
                Text(
                    text = stringResource(R.string.membership_hero_trial_price, trialDays),
                    style = MaterialTheme.typography.headlineLarge.copy(
                        fontWeight = FontWeight.ExtraBold,
                        fontSize = 34.sp,
                    ),
                    color = Color.White,
                    textAlign = TextAlign.Center,
                    maxLines = 1,
                    modifier = Modifier.fillMaxWidth(),
                )
                Spacer(Modifier.height(4.dp))
                // "Then X Kč/month" for monthly, "Then X Kč/year" for annual.
                // Annual intentionally has no per-month split so we don't show
                // a rounded number that doesn't match what Stripe charges.
                Text(
                    text = stringResource(
                        if (isAnnual) R.string.membership_hero_then_price_year
                        else R.string.membership_hero_then_price,
                        formatPriceCzk(regularPrice),
                    ),
                    style = MaterialTheme.typography.bodyMedium,
                    color = Color.White.copy(alpha = 0.7f),
                    textDecoration = TextDecoration.LineThrough,
                    textAlign = TextAlign.Center,
                    modifier = Modifier.fillMaxWidth(),
                )
            } else {
                Text(
                    text = stringResource(
                        regularPriceLabelRes,
                        formatPriceCzk(regularPrice),
                    ),
                    style = MaterialTheme.typography.headlineLarge.copy(
                        fontWeight = FontWeight.ExtraBold,
                        fontSize = 32.sp,
                    ),
                    color = Color.White,
                    textAlign = TextAlign.Center,
                    maxLines = 1,
                    modifier = Modifier.fillMaxWidth(),
                )
            }

            Spacer(Modifier.height(16.dp))

            // Plan switcher — embedded in the hero so the user picks before
            // ever scrolling. Light pill on dark background reads correctly
            // against the gradient.
            if (plans.size >= 2) {
                PlanSwitcherDark(
                    plans = plans,
                    selectedCode = selectedPlanCode,
                    onSelect = onSelectPlan,
                )
            }
            // Bottom space sized to the mascot height so the switcher and
            // mascot share a horizontal band — mascot's vertical center sits
            // roughly at the switcher's vertical center, so they read as
            // companions, not stacked.
            Spacer(Modifier.height(56.dp))
        }

        // Mascot — bottom-end overlay. Smaller (96dp) and tucked closer to the
        // edge so it visually anchors next to the plan switcher rather than
        // floating in negative space. Vertical alignment lands roughly at the
        // switcher's vertical center thanks to the 56dp spacer above.
        Image(
            painter = painterResource(R.drawable.mascot_waving),
            contentDescription = null,
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .padding(end = 12.dp, bottom = 4.dp)
                .size(96.dp),
        )
    }
}

/**
 * Pill-style plan switcher tuned for dark hero. Compact sizing — wraps to
 * content width and centers under the price block instead of stretching to
 * fill. Savings indicator renders as a small inline pill next to the label
 * (not a vertical stack) so the segments stay short and balanced.
 */
@Composable
private fun PlanSwitcherDark(
    plans: List<cz.cleansia.customer.core.memberships.MembershipPlanDto>,
    selectedCode: String,
    onSelect: (String) -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 24.dp),
        horizontalArrangement = Arrangement.Center,
    ) {
        Row(
            modifier = Modifier
                .clip(RoundedCornerShape(24.dp))
                .background(Color.White.copy(alpha = 0.10f))
                .padding(3.dp),
            horizontalArrangement = Arrangement.spacedBy(3.dp),
        ) {
            plans.forEach { plan ->
                val selected = plan.code == selectedCode
                val labelRes = if (plan.billingInterval == 2) {
                    R.string.membership_plan_annual
                } else {
                    R.string.membership_plan_monthly
                }
                Row(
                    modifier = Modifier
                        .clip(RoundedCornerShape(20.dp))
                        .background(if (selected) Sky400 else Color.Transparent)
                        .clickable { onSelect(plan.code) }
                        .padding(horizontal = 16.dp, vertical = 8.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    Text(
                        text = stringResource(labelRes),
                        style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                        color = if (selected) Slate900 else Color.White,
                    )
                    if (plan.savingsPercentVsMonthly > 0.0) {
                        // Inline savings pill — much smaller than the vertical
                        // stacked variant. Reads as "Annual · -15%" rather than
                        // shouting "SAVE 15%".
                        Box(
                            modifier = Modifier
                                .clip(RoundedCornerShape(6.dp))
                                .background(
                                    if (selected) Slate900.copy(alpha = 0.15f)
                                    else Sky400.copy(alpha = 0.20f),
                                )
                                .padding(horizontal = 6.dp, vertical = 2.dp),
                        ) {
                            Text(
                                text = "−${plan.savingsPercentVsMonthly.toInt()}%",
                                style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                                color = if (selected) Slate900 else Sky400,
                            )
                        }
                    }
                }
            }
        }
    }
}

/**
 * Stat tile under the hero — "Members typically save X Kč per cleaning".
 * Number is currently hardcoded as a marketing claim; once we have real
 * analytics on member discount realization, source it from the backend.
 */
@Composable
private fun SocialProofTile() {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp)
            .clip(RoundedCornerShape(16.dp))
            .background(Sky400.copy(alpha = 0.12f))
            .padding(horizontal = 16.dp, vertical = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(40.dp)
                .clip(CircleShape)
                .background(Sky400.copy(alpha = 0.20f)),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.TrendingUp,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(22.dp),
            )
        }
        Spacer(Modifier.width(14.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = stringResource(R.string.membership_social_proof_headline),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.primary,
            )
            Text(
                text = stringResource(R.string.membership_social_proof_sub),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun PerksHeader() {
    Text(
        text = stringResource(R.string.membership_perks_section_title),
        style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
        color = MaterialTheme.colorScheme.onBackground,
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp),
    )
}

@Composable
private fun PerkTile(icon: ImageVector, title: String, desc: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 20.dp)
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(16.dp),
        verticalAlignment = Alignment.Top,
    ) {
        Box(
            modifier = Modifier
                .size(44.dp)
                .clip(CircleShape)
                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(24.dp),
            )
        }
        Spacer(Modifier.width(14.dp))
        Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
            Text(
                text = title,
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                text = desc,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

/**
 * Sticky CTA at the bottom — full-width primary action + fine-print renewal
 * terms. Sits above the system navigation bar so it's never obscured.
 */
@Composable
private fun StickyCtaBar(
    modifier: Modifier = Modifier,
    ctaLabel: String,
    disclosure: String,
    enabled: Boolean,
    onClick: () -> Unit,
) {
    Column(
        modifier = modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.surface)
            .windowInsetsPadding(WindowInsets.navigationBars)
            .padding(horizontal = 20.dp, vertical = 14.dp),
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(54.dp)
                .clip(RoundedCornerShape(28.dp))
                .background(if (enabled) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.primary.copy(alpha = 0.4f))
                .clickable(enabled = enabled, onClick = onClick),
            contentAlignment = Alignment.Center,
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    Icons.Outlined.WorkspacePremium,
                    contentDescription = null,
                    tint = Color.White,
                    modifier = Modifier.size(20.dp),
                )
                Spacer(Modifier.width(8.dp))
                Text(
                    text = ctaLabel,
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = Color.White,
                )
            }
        }
        Spacer(Modifier.height(8.dp))
        Text(
            text = disclosure,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier.fillMaxWidth(),
        )
    }
}

/**
 * Build the fine-print disclosure under the CTA. Trial-aware: when the plan
 * has a trial, lead with "Then X Kč/month, cancel anytime"; otherwise the
 * plain "Cancel anytime" disclosure.
 */
@Composable
private fun buildDisclosure(plan: cz.cleansia.customer.core.memberships.MembershipPlanDto?): String {
    if (plan == null) return stringResource(R.string.membership_disclosure)
    if (plan.trialPeriodDays <= 0) return stringResource(R.string.membership_disclosure)
    // Trial-aware disclosure. Annual variant uses year price; monthly uses
    // per-month — mirrors the hero block's billing-interval split so the
    // user never sees a per-month figure for an annual plan.
    val resId = if (plan.billingInterval == 2) {
        R.string.membership_cta_disclosure_trial_year
    } else {
        R.string.membership_cta_disclosure_trial
    }
    return stringResource(resId, formatPriceCzk(plan.price))
}

/**
 * Format a CZK amount for display. Drops the decimal when the price is a whole
 * number (199 Kč rather than 199.00 Kč) — matches the rest of the app's
 * money-display convention.
 */
private fun formatPriceCzk(amount: Double): String {
    val rounded = if (amount % 1.0 == 0.0) amount.toInt().toString() else "%.2f".format(amount)
    return "$rounded Kč"
}

/**
 * Snackbar pulled out of the Hilt graph at composition time. Same pattern
 * as the BookingSheetEntryPoint — the screen needs the singleton snackbar
 * controller for PaymentSheet result handling without going through a VM.
 */
@dagger.hilt.EntryPoint
@dagger.hilt.InstallIn(dagger.hilt.components.SingletonComponent::class)
interface SubscribePlusEntryPoint {
    fun snackbarController(): SnackbarController
}
