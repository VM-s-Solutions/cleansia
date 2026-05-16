package cz.cleansia.customer.ui.components

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.MutableTransitionState
import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.animation.scaleOut
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R

/**
 * Full-screen busy overlay with the cleaning mascot. Used by long async ops
 * (booking submit, payment, subscribe Plus) so the user sees mascot motion
 * instead of a bare spinner. Drop into any screen as a sibling of the main
 * content; visibility is driven by [visible].
 *
 * The overlay swallows touches so the user can't accidentally interact with
 * the underlying screen mid-flight (no double-submits). The dim layer fades
 * in/out and the mascot card springs in from below — same choreography as
 * [CleansiaDialog] for visual consistency.
 *
 * [message] is the headline text under the mascot — keep it short ("Booking
 * your cleaning…", "Processing payment…"). Pass localized strings; the
 * composable doesn't read R.string itself.
 */
@Composable
fun BusyMascotOverlay(
    visible: Boolean,
    message: String,
    modifier: Modifier = Modifier,
) {
    if (!visible) return

    val state = remember { MutableTransitionState(false) }
        .apply { targetState = true }

    Box(
        modifier = modifier
            .fillMaxSize()
            // Block all touches under the overlay. Empty interactionSource +
            // null indication = no ripple, just the click-swallow effect.
            .clickable(
                interactionSource = remember { MutableInteractionSource() },
                indication = null,
                onClick = {},
            )
            .background(Color.Black.copy(alpha = 0.45f)),
        contentAlignment = Alignment.Center,
    ) {
        AnimatedVisibility(
            visibleState = state,
            enter = scaleIn(
                animationSpec = spring(
                    dampingRatio = Spring.DampingRatioMediumBouncy,
                    stiffness = Spring.StiffnessMediumLow,
                ),
                initialScale = 0.85f,
            ) + fadeIn(animationSpec = tween(durationMillis = 220)),
            exit = fadeOut(animationSpec = tween(durationMillis = 120)) +
                scaleOut(
                    animationSpec = tween(durationMillis = 120),
                    targetScale = 0.95f,
                ),
        ) {
            Surface(
                modifier = Modifier
                    .padding(horizontal = 32.dp)
                    .widthIn(max = 360.dp)
                    .fillMaxWidth(),
                shape = RoundedCornerShape(24.dp),
                color = MaterialTheme.colorScheme.surface,
                tonalElevation = 8.dp,
                shadowElevation = 16.dp,
            ) {
                Column(
                    modifier = Modifier.padding(horizontal = 24.dp, vertical = 28.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                ) {
                    MascotAnimation(
                        resId = R.raw.mascot_cleaning_in_progress,
                        size = 140.dp,
                    )
                    Spacer(Modifier.height(8.dp))
                    Text(
                        text = message,
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                        color = MaterialTheme.colorScheme.onSurface,
                        textAlign = TextAlign.Center,
                    )
                }
            }
        }
    }
}
