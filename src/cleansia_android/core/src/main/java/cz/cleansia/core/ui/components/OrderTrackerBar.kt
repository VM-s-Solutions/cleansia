package cz.cleansia.core.ui.components

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.layout
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

/** Cancelled reads red rather than brand — it is the one phase that is not progress. */
private val DangerCancel = Color(0xFFDC2626)

/**
 * Segmented progress bar, one thin bar per phase: past solid, current animating, future muted.
 *
 * **No shimmer, no looping, no halo** — the bar is intentionally quiet so the reader's eye stays on the
 * job, not the chrome.
 *
 * It takes a step INDEX rather than an order status because the two apps do not share a status type:
 * the partner reads the generated `OrderStatus` off its own NSwag client, the customer a hand-written
 * presentation enum. Each maps its own statuses to an index and hands over the already-translated
 * counter label, which also keeps this file free of an `R` it cannot reach from `:core`.
 *
 * @param currentStep 0-based index of the phase in flight. At or past [totalSteps] every segment is drawn as past.
 * @param stepCounterLabel already-formatted and translated, e.g. "Step 3 / 5". Null hides the counter.
 * @param cancelled draws the single full-width red bar instead — a dead order has no progress to show.
 */
@Composable
fun OrderTrackerBar(
    currentStep: Int,
    stepCounterLabel: String?,
    modifier: Modifier = Modifier,
    totalSteps: Int = 5,
    cancelled: Boolean = false,
    cancelledLabel: String? = null,
) {
    if (cancelled) {
        CancelledFullBar(label = cancelledLabel, modifier = modifier)
        return
    }

    // All segments live on the brand palette. Completed and past phases used to read green to signal
    // "done", but a single-color tracker reads cleaner — the past/current distinction comes from
    // saturation, not hue.
    val activeColor = MaterialTheme.colorScheme.primary
    val muted = MaterialTheme.colorScheme.outlineVariant
    val allPast = currentStep >= totalSteps

    // No left-side step name — the headline above the bar already carries it, and repeating it here was
    // visual double-billing. Only the right-aligned counter remains, tucked tight above the bar so the
    // whole tracker reads as a single sub-element under the headline rather than a separate block.
    Column(modifier = modifier.fillMaxWidth()) {
        if (stepCounterLabel != null) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.End,
            ) {
                Text(
                    text = stepCounterLabel,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Spacer(Modifier.height(4.dp))
        }
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(6.dp),
        ) {
            repeat(totalSteps) { index ->
                ProgressSegment(
                    state = when {
                        allPast || index < currentStep -> SegmentState.Past
                        index == currentStep -> SegmentState.Current
                        else -> SegmentState.Future
                    },
                    activeColor = activeColor,
                    mutedColor = muted,
                    modifier = Modifier.weight(1f),
                )
            }
        }
    }
}

private enum class SegmentState { Past, Current, Future }

/**
 * One thin pill bar: past solid, future muted, current an indeterminate band sliding across a pale
 * tinted track.
 *
 * The background tint is what stops the current bar reading as inactive between sweeps.
 */
@Composable
private fun ProgressSegment(
    state: SegmentState,
    activeColor: Color,
    mutedColor: Color,
    modifier: Modifier = Modifier,
) {
    val shape = RoundedCornerShape(2.dp)

    when (state) {
        SegmentState.Past -> Box(
            modifier = modifier
                .height(4.dp)
                .clip(shape)
                .background(activeColor),
        )
        SegmentState.Future -> Box(
            modifier = modifier
                .height(4.dp)
                .clip(shape)
                .background(mutedColor),
        )
        SegmentState.Current -> {
            val transition = rememberInfiniteTransition(label = "currentSegmentSweep")
            // A single sweep with a deliberate rest beat: the phase travels past 1f so the band sits
            // off-screen for a pause before wrapping, rather than restarting the instant it exits.
            val sweepWidthFraction = 0.5f
            // Phase range: the visible portion is [-0.5, 1.0] (1.5 units) and the rest beat is
            // [1.0, 2.5] (1.5 units). Equal halves means the sweep is visible for half the cycle and the
            // segment sits in its armed-track tint for the other half — at 1300ms that is a ~650ms sweep
            // and a ~650ms breather between passes.
            val travelEnd = 2.5f
            val phase by transition.animateFloat(
                initialValue = -sweepWidthFraction,
                targetValue = travelEnd,
                animationSpec = infiniteRepeatable(
                    animation = tween(durationMillis = 1300, easing = LinearEasing),
                    repeatMode = RepeatMode.Restart,
                ),
                label = "currentSegmentPhase",
            )

            // The sweep is a horizontal gradient fading transparent → very soft activeColor across its
            // width. Peak alpha is kept low (~0.30) so the band reads as a faint glow gliding over the
            // armed track instead of a saturated wipe — quiet enough to live on screen for hours.
            val sweepBrush = Brush.horizontalGradient(
                colors = listOf(
                    activeColor.copy(alpha = 0f),
                    activeColor.copy(alpha = 0.18f),
                    activeColor.copy(alpha = 0.30f),
                ),
            )

            Box(
                modifier = modifier
                    .height(4.dp)
                    .clip(shape)
                    // Armed-track tint behind the sweep so the bar never looks dead between passes.
                    .background(activeColor.copy(alpha = 0.22f)),
            ) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth(fraction = sweepWidthFraction)
                        .fillMaxHeight()
                        .offsetXFraction(phase)
                        .background(sweepBrush),
                )
            }
        }
    }
}

/**
 * Offsets a child horizontally by [fraction] of its parent's width. Negative fractions push left of the
 * parent, > 1 push right past it. Compose has no built-in fractional offset modifier, so this layout
 * pass does it directly.
 */
private fun Modifier.offsetXFraction(fraction: Float): Modifier = this.layout { measurable, constraints ->
    val placeable = measurable.measure(constraints)
    layout(placeable.width, placeable.height) {
        val parentWidth = constraints.maxWidth
        placeable.placeRelative(
            x = (parentWidth * fraction).toInt(),
            y = 0,
        )
    }
}

@Composable
private fun CancelledFullBar(label: String?, modifier: Modifier = Modifier) {
    Column(modifier = modifier.fillMaxWidth()) {
        if (label != null) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                Text(
                    text = label,
                    style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                    color = DangerCancel,
                )
            }
            Spacer(Modifier.height(8.dp))
        }
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(4.dp)
                .clip(RoundedCornerShape(2.dp))
                .background(DangerCancel),
        )
    }
}
