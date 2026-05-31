package cz.cleansia.partner.features.orders.components

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.animateContentSize
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.OrderStatus
import kotlinx.coroutines.delay

/**
 * In-sheet hero card carrying the phase-appropriate big number
 * (timer / countdown / scheduled date), a one-line headline above it
 * and a one-line subtitle below. Styled to match the other section
 * cards (white surface, 16dp radius, tonalElevation 1dp) so the
 * tracker reads as a sibling in the sheet stack rather than a
 * gradient experiment fighting them.
 *
 * Mascot moved out of this card — it's a separate floating element
 * (see [FloatingMascot]) anchored to the sheet edge.
 */
@Composable
fun OrderTimerCard(
    order: OrderItem,
    status: OrderStatus?,
    startedAtEpochMillis: Long?,
    modifier: Modifier = Modifier,
) {
    if (status == OrderStatus._6) return

    val scheduledMillis = remember(order.cleaningDateTime) {
        order.cleaningDateTime
            ?.takeIf { it.isNotBlank() }
            ?.let { runCatching { java.time.Instant.parse(it).toEpochMilli() }.getOrNull() }
    }

    // Phase-aware ticker:
    //   - InProgress: 1Hz so the live timer doesn't lurch.
    //   - Confirmed: 1/min so the countdown refreshes naturally.
    //   - Other phases: idle (saves battery on stable orders).
    val tickIntervalMs: Long? = when (status) {
        OrderStatus._4 -> 1_000L
        OrderStatus._2 -> 60_000L
        else -> null
    }
    var nowMillis by remember { mutableLongStateOf(System.currentTimeMillis()) }
    LaunchedEffect(tickIntervalMs, status) {
        val interval = tickIntervalMs ?: return@LaunchedEffect
        while (true) {
            nowMillis = System.currentTimeMillis()
            delay(interval)
        }
    }

    // Plain surface — no tonal elevation. tonalElevation lifts the
    // primary color into the surface, which gave the card its blue
    // tint; flat surface keeps it neutral so the brand color reads
    // only on the actual numbers / pacing pill.
    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface,
        tonalElevation = 0.dp,
    ) {
        Column(
            modifier = Modifier
                // Inner padding matches [OrderMetadataRow]'s 4dp inset
                // so the timer text, the order number, and the date
                // share a single left edge. Asymmetric vertical: 12dp
                // top for breathing room under the drag handle, 0dp
                // bottom so the timer text sits flush against the
                // segmented progress bar that's rendered directly
                // below it in the parent column.
                .padding(PaddingValues(start = 4.dp, end = 4.dp, top = 12.dp, bottom = 0.dp))
                .animateContentSize(),
            verticalArrangement = Arrangement.Center,
        ) {
            // Two-line card now: "Cleaning" eyebrow + the live timer.
            // The pacing subtitle ("X ahead of schedule" / "X over"
            // etc.) was removed — too narrative for a sheet that
            // already shows the workflow status via the segmented
            // progress bar right below this card. Cleaner can derive
            // pacing from the timer + the scheduled time in the
            // metadata row without us editorializing.
            TimerHeadline(
                status = status,
                scheduledMillis = scheduledMillis,
                nowMillis = nowMillis,
            )
            Spacer(Modifier.height(2.dp))
            TimerPrimaryText(
                status = status,
                scheduledMillis = scheduledMillis,
                startedAtMillis = startedAtEpochMillis,
                nowMillis = nowMillis,
            )
            // On terminal Completed orders, surface the authoritative
            // completion timestamp directly under the timer text.
            // The primary text still shows "Completed in Xh Ym"
            // (the duration the cleaner usually cares about); this
            // sub-line answers "and *when* did I finish?" without
            // any derivation — comes straight from order.completedAt
            // (DB column, set inside the domain at completion).
            if (status == OrderStatus._5 && !order.completedAt.isNullOrBlank()) {
                Spacer(Modifier.height(2.dp))
                Text(
                    text = stringResource(
                        R.string.tracker_finished_at,
                        formatOrderDateTime(order.completedAt) ?: "—",
                    ),
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

@Composable
private fun TimerHeadline(
    status: OrderStatus?,
    scheduledMillis: Long?,
    nowMillis: Long,
) {
    val text: String = when (status) {
        OrderStatus._0, OrderStatus._1 -> stringResource(R.string.tracker_headline_new)
        OrderStatus._2 -> {
            val remainingMs = (scheduledMillis ?: 0L) - nowMillis
            if (scheduledMillis != null && remainingMs in 0 until 30 * 60_000L) {
                stringResource(R.string.tracker_headline_confirmed_soon)
            } else {
                stringResource(R.string.tracker_headline_confirmed)
            }
        }
        OrderStatus._3 -> stringResource(R.string.tracker_headline_on_the_way)
        OrderStatus._4 -> stringResource(R.string.tracker_headline_in_progress)
        OrderStatus._5 -> stringResource(R.string.tracker_headline_done)
        else -> ""
    }
    if (text.isEmpty()) return
    AnimatedContent(
        targetState = text,
        transitionSpec = { fadeIn(tween(300)) togetherWith fadeOut(tween(300)) },
        label = "timerHeadlineCrossfade",
    ) { current ->
        Text(
            text = current,
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun TimerPrimaryText(
    status: OrderStatus?,
    scheduledMillis: Long?,
    startedAtMillis: Long?,
    nowMillis: Long,
) {
    val text: String = when (status) {
        OrderStatus._0, OrderStatus._1 ->
            formatOrderDateTime(toIso(scheduledMillis)) ?: "—"
        OrderStatus._2 -> {
            val remainingMs = (scheduledMillis ?: 0L) - nowMillis
            if (scheduledMillis != null && remainingMs > 0) {
                stringResource(
                    R.string.tracker_countdown_starts_in,
                    formatHoursMinutesDuration(totalMinutes = (remainingMs / 60_000L).toInt()),
                )
            } else {
                formatOrderDateTime(toIso(scheduledMillis)) ?: "—"
            }
        }
        OrderStatus._3 ->
            stringResource(
                R.string.tracker_subtitle_on_the_way_arriving,
                formatOrderTime(toIso(scheduledMillis)) ?: "—",
            )
        OrderStatus._4 ->
            formatElapsedClock(((nowMillis - (startedAtMillis ?: nowMillis)).coerceAtLeast(0L)))
        OrderStatus._5 -> {
            val durationMillis = if (startedAtMillis != null) {
                ((nowMillis.coerceAtLeast(startedAtMillis)) - startedAtMillis)
                    .coerceAtLeast(0L)
            } else 0L
            stringResource(
                R.string.tracker_completed_in,
                formatHoursMinutesDuration(totalMinutes = (durationMillis / 60_000L).toInt()),
            )
        }
        else -> "—"
    }
    // Bumped to headlineMedium so the live timer is the unambiguous
    // hero of the sheet — same platform sans-serif everywhere else
    // uses, just one step heavier in the hierarchy. SemiBold +
    // tightened tracking keeps the digits feeling like a chronograph
    // without dropping into a monospace font.
    Text(
        text = text,
        style = MaterialTheme.typography.headlineMedium.copy(
            fontWeight = FontWeight.SemiBold,
            letterSpacing = (-0.5).sp,
        ),
        color = MaterialTheme.colorScheme.onSurface,
        maxLines = 1,
    )
}

// ─── Format helpers ────────────────────────────────────────────────

private fun toIso(epochMillis: Long?): String? =
    epochMillis?.let { java.time.Instant.ofEpochMilli(it).toString() }

/** "1:42:08" past an hour, "02:08" for shorter jobs. */
private fun formatElapsedClock(millis: Long): String {
    val totalSeconds = millis / 1_000L
    val hours = totalSeconds / 3_600L
    val minutes = (totalSeconds % 3_600L) / 60L
    val seconds = totalSeconds % 60L
    return if (hours > 0) {
        "%d:%02d:%02d".format(hours, minutes, seconds)
    } else {
        "%02d:%02d".format(minutes, seconds)
    }
}

@Composable
private fun formatHoursMinutesDuration(totalMinutes: Int): String {
    val hours = totalMinutes / 60
    val minutes = totalMinutes % 60
    return if (hours > 0) {
        stringResource(R.string.duration_hours_minutes, hours, minutes)
    } else {
        stringResource(R.string.duration_minutes_only, minutes)
    }
}
