package cz.cleansia.partner.features.orders.components

import androidx.compose.animation.core.FastOutSlowInEasing
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
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderStatus

/**
 * Foodora-style segmented progress bar. Five thin separated bars
 * (one per phase). Past phases are solid green, the current phase
 * animates its fill once on entry, future phases sit muted.
 *
 * No shimmer, no looping, no halo — Foodora's bar is intentionally
 * quiet so the cleaner's eye stays on content, not on the chrome.
 * The single fill-in animation is enough to acknowledge the phase
 * transition without becoming visual noise on a multi-hour job.
 *
 * Cancelled gets a flat danger-red bar (no segmentation — the
 * workflow didn't progress, so segments would be misleading).
 */
@Composable
fun ContinuousProgressBar(
    status: OrderStatus?,
    modifier: Modifier = Modifier,
) {
    val brand = MaterialTheme.colorScheme.primary
    val muted = MaterialTheme.colorScheme.outlineVariant

    if (status == OrderStatus._6) {
        CancelledFullBar(modifier = modifier)
        return
    }

    val currentIndex = when (status) {
        OrderStatus._0, OrderStatus._1 -> 0
        OrderStatus._2 -> 1
        OrderStatus._3 -> 2
        OrderStatus._4 -> 3
        OrderStatus._5 -> 4
        else -> 0
    }
    val isCompleted = status == OrderStatus._5
    // All segments live on the brand-blue palette now — completed
    // and past phases used to read green to signal "done", but a
    // single-color tracker reads cleaner and matches Foodora's
    // pattern (the past/current distinction comes from saturation,
    // not hue).
    val activeColor = brand

    // No left-side step name — that's already the timer card's
    // eyebrow above this composable; repeating it here was visual
    // double-billing. Only the right-aligned step counter remains,
    // tucked tight above the bar so the whole tracker reads as a
    // single sub-element under the timer rather than a separate
    // labelled block with its own breathing room.
    Column(modifier = modifier.fillMaxWidth()) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.End,
        ) {
            Text(
                text = stringResource(
                    R.string.tracker_step_counter,
                    if (isCompleted) 5 else currentIndex + 1,
                    5,
                ),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Spacer(Modifier.height(4.dp))
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(6.dp),
        ) {
            repeat(5) { index ->
                ProgressSegment(
                    state = when {
                        isCompleted -> SegmentState.Past
                        index < currentIndex -> SegmentState.Past
                        index == currentIndex -> SegmentState.Current
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
 * One thin pill bar. 4dp tall — Foodora's tracker reads small and
 * confident. Past = solid past-color, Future = muted, Current =
 * Windows-style indeterminate: pale active-tinted track with a
 * brighter active-color band that slides L → R across it on a
 * ~1.5s loop.
 *
 * Implementation:
 *  - Background fill is `activeColor * 0.25 alpha` — gives the bar
 *    its "armed" tint so it doesn't read as inactive between sweeps.
 *  - A 40%-wide band sits on top, full-opacity activeColor, clipped
 *    to the segment via the parent's clip. Its X is animated with
 *    `offsetXFraction` from `-0.4f` to `1.0f` so it enters from the
 *    left edge and exits past the right edge before looping.
 *  - Linear easing — Windows progress bars don't ease; the band
 *    travels at constant speed. Easing in/out would feel sluggish.
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
            // Single sweep with an intentional rest beat between
            // passes. Phase travels from -sweepWidth to a value
            // greater than 1f — the extra range past 1f keeps the
            // sweep off-screen on the right for a noticeable pause
            // before wrapping. With LinearEasing the rest is just
            // the segment sitting in its armed-track tint, then the
            // next sweep enters from the left.
            //
            // travelEnd = 1f → sweep right-edge hits the segment's
            //                  right edge then immediately wraps.
            // travelEnd = 2f → sweep finishes, then we spend the
            //                  same amount of time again with the
            //                  sweep off-screen before restarting.
            val sweepWidthFraction = 0.5f
            // Phase range: visible portion is [-0.5, 1.0] (1.5
            // units), rest beat is [1.0, 2.5] (1.5 units) — equal
            // halves means the sweep is visible for half the cycle
            // and the segment sits in its armed-track tint for the
            // other half. With 1300ms total, the visible sweep
            // takes ~650ms (faster than before) and the pause
            // before the next sweep is ~650ms (clear breather
            // between passes).
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

            // Sweep is a horizontal gradient that fades transparent
            // → very soft activeColor across its width. Peak alpha
            // kept low (~0.30) so the band reads as a faint glow
            // gliding over the armed track instead of a saturated
            // wipe — quiet enough to live on screen for hours
            // without becoming visual noise.
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
                    // Armed-track tint behind the sweep so the bar
                    // never looks dead between passes.
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
 * Offsets a child horizontally by `fraction` of its parent's width.
 * Negative fractions push left of the parent, > 1 push right past it.
 * Compose has no built-in fractional offset modifier, so this layout
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
private fun CancelledFullBar(modifier: Modifier = Modifier) {
    Column(modifier = modifier.fillMaxWidth()) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Text(
                text = stringResource(R.string.status_cancelled),
                style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                color = DangerCancel,
            )
        }
        Spacer(Modifier.height(8.dp))
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(4.dp)
                .clip(RoundedCornerShape(2.dp))
                .background(DangerCancel),
        )
    }
}

private fun stepNameRes(status: OrderStatus?): Int = when (status) {
    OrderStatus._0, OrderStatus._1 -> R.string.tracker_step_new
    OrderStatus._2 -> R.string.tracker_step_confirmed
    OrderStatus._3 -> R.string.tracker_step_on_way
    OrderStatus._4 -> R.string.tracker_step_cleaning
    OrderStatus._5 -> R.string.tracker_step_done
    OrderStatus._6 -> R.string.status_cancelled
    else -> R.string.tracker_step_new
}

private val DangerCancel = Color(0xFFDC2626)
