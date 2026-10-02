package cz.cleansia.customer.features.membership

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.systemBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.outlined.Bolt
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.LocalOffer
import androidx.compose.material.icons.outlined.Repeat
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.customer.R
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.customer.ui.theme.SuccessText

/**
 * Post-purchase celebration screen — replaces the silent snackbar +
 * popBackStack flow that left users staring at a blank Subscribe screen
 * for a beat after Stripe confirmed.
 *
 * Two CTAs:
 *  - Primary: "Book your first cleaning" → home (or directly to booking
 *    sheet via the home FAB).
 *  - Secondary: "Set up recurring" → recurring create wizard. Only visible
 *    once because it's the headline Plus perk; everything else they can
 *    discover on their own from the Profile tab.
 *
 * A trialing member has every benefit from day one, and is told when the free trial ends.
 * -> /product/business-rules
 */
@Composable
fun MembershipSuccessScreen(
    onPrimary: () -> Unit,
    onSecondary: () -> Unit,
    viewModel: MembershipViewModel = hiltViewModel(),
) {
    val trialEndsAt by viewModel.trialEndsAt.collectAsStateWithLifecycle()
    val membership by viewModel.current.collectAsStateWithLifecycle()
    val discountPercent = membership?.discountPercentage?.toInt() ?: 0

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(
                Brush.verticalGradient(
                    listOf(
                        MaterialTheme.colorScheme.primary.copy(alpha = 0.10f),
                        MaterialTheme.colorScheme.background,
                    ),
                ),
            )
            // The screen's own system-bar insets, as on the booking confirmation (C16): no transparent
            // mascot top keeps the content clear of the status bar any more.
            .systemBarsPadding()
            .verticalScroll(rememberScrollState()),
        // Center vertically — content fits on a typical viewport so it should
        // sit in the middle of the screen, not pinned to the top. The
        // verticalScroll wrapper above keeps it usable on shorter screens
        // (tall content still scrolls; short content centers).
        contentAlignment = Alignment.Center,
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp, vertical = 24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            // The booking confirmation's success cue (C16): a 48dp check in place of the 200dp mascot.
            // Decorative: the title under it says the same thing.
            Icon(
                imageVector = Icons.Filled.CheckCircle,
                contentDescription = null,
                tint = SuccessText,
                modifier = Modifier.size(48.dp),
            )
            Spacer(Modifier.height(8.dp))

            Text(
                text = stringResource(R.string.membership_success_title),
                style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onBackground,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(6.dp))
            Text(
                text = trialEndsAt?.let {
                    stringResource(R.string.membership_success_subtitle_trial, formatPeriodEnd(it.toString()))
                } ?: stringResource(R.string.membership_success_subtitle),
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )

            Spacer(Modifier.height(24.dp))

            // Compact perk preview — same icons as the subscribe screen so
            // users recognize what they just unlocked. Kept to 4 items so the
            // screen doesn't become a scroll-fest.
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(16.dp))
                    .background(MaterialTheme.colorScheme.surface)
                    .border(
                        1.dp,
                        MaterialTheme.colorScheme.outlineVariant,
                        RoundedCornerShape(16.dp),
                    )
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(14.dp),
            ) {
                Text(
                    text = stringResource(R.string.membership_success_perks_header),
                    style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                if (discountPercent > 0) {
                    PerkRow(
                        icon = Icons.Outlined.LocalOffer,
                        title = stringResource(R.string.membership_perk_discount_title, discountPercent),
                    )
                }
                PerkRow(
                    icon = Icons.Outlined.Repeat,
                    title = stringResource(R.string.membership_perk_recurring_title),
                )
                PerkRow(
                    icon = Icons.Outlined.CheckCircle,
                    title = stringResource(R.string.membership_perk_cancellation_title),
                )
                PerkRow(
                    icon = Icons.Outlined.Bolt,
                    title = stringResource(R.string.membership_success_perk_express),
                )
            }

            Spacer(Modifier.height(28.dp))

            // Primary action — pull users into the headline benefit.
            CleansiaPrimaryButton(
                onClick = onSecondary,
                text = stringResource(R.string.membership_success_cta_setup_recurring),
                modifier = Modifier.fillMaxWidth(),
            )
            Spacer(Modifier.height(8.dp))
            // Secondary — plain "I'm done here" exit. TextButton-style so the
            // recurring CTA stays the dominant choice.
            androidx.compose.material3.TextButton(
                onClick = onPrimary,
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text(
                    text = stringResource(R.string.membership_success_cta_back_home),
                    style = MaterialTheme.typography.titleMedium,
                )
            }

            Spacer(Modifier.height(24.dp))
        }
    }
}

@Composable
private fun PerkRow(icon: ImageVector, title: String) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Box(
            modifier = Modifier
                .size(36.dp)
                .clip(CircleShape)
                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Text(
            text = title,
            style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}
